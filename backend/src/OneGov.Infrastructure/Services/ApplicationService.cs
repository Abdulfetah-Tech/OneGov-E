using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using OneGov.Application.Applications;
using OneGov.Application.Audit;
using OneGov.Application.Common;
using OneGov.Domain.Applications;
using OneGov.Domain.Common;
using OneGov.Domain.Identity;
using OneGov.Domain.Registry;
using OneGov.Domain.Workflow;
using OneGov.Infrastructure.Persistence;

namespace OneGov.Infrastructure.Services;

public sealed class ApplicationService(
    OneGovDbContext db,
    IAuditLog audit,
    TimeProvider timeProvider,
    IValidator<CreateApplicationRequest> createValidator,
    IValidator<ListApplicationsQuery> listValidator,
    IValidator<TransitionRequest> transitionValidator) : IApplicationService
{
    private const int MaxPayloadBytes = 64 * 1024;

    public async Task<Result<ApplicationDetailDto>> CreateDraftAsync(
        Actor actor, CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ApplicationDetailDto>.Failure(Invalid(validation));
        }

        if (!actor.Has(Permissions.ApplicationSubmit))
        {
            return Result<ApplicationDetailDto>.Failure(ApplicationErrors.Forbidden, "You are not allowed to apply for services.");
        }

        var code = request.ServiceCode.Trim().ToUpperInvariant();
        var service = await db.Services.SingleOrDefaultAsync(s => s.Code == code, cancellationToken);
        if (service is null)
        {
            return Result<ApplicationDetailDto>.Failure(ApplicationErrors.ServiceNotFound, "That service does not exist.");
        }

        var workflow = service.Status == ServiceStatus.Available && service.WorkflowCode is not null
            ? BuiltInWorkflows.Latest(service.WorkflowCode)
            : null;
        if (workflow is null)
        {
            return Result<ApplicationDetailDto>.Failure(ApplicationErrors.ServiceUnavailable, "That service is not open for applications.");
        }

        var application = ServiceApplication.CreateDraft(service.Id, service.AgencyId, actor.UserId, workflow);
        db.Applications.Add(application);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(actor, "application.created", application.Id, cancellationToken);

        return Result<ApplicationDetailDto>.Success(ToDetail(application, service.Code, workflow, actor));
    }

    public async Task<Result<PagedResult<ApplicationSummaryDto>>> ListAsync(
        ListApplicationsQuery query, CancellationToken cancellationToken)
    {
        var validation = await listValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<PagedResult<ApplicationSummaryDto>>.Failure(Invalid(validation));
        }

        // The data scope filter on the context limits this to what the caller may see.
        var applications = db.Applications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.State))
        {
            var state = query.State.Trim();
            applications = applications.Where(a => a.State == state);
        }

        var rows =
            from a in applications
            join s in db.Services on a.ServiceId equals s.Id
            orderby a.UpdatedAt descending, a.Id descending
            select new ApplicationSummaryDto(a.Id, a.ReferenceNumber, s.Code, a.State, a.CreatedAt, a.UpdatedAt);

        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return Result<PagedResult<ApplicationSummaryDto>>.Success(new PagedResult<ApplicationSummaryDto>(items, query.Page, query.PageSize, total));
    }

    public async Task<Result<ApplicationDetailDto>> GetAsync(Actor actor, Guid id, CancellationToken cancellationToken)
    {
        var application = await db.Applications.AsNoTracking().Include(a => a.History)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        return application is null ? NotFound() : await BuildDetailAsync(application, actor, cancellationToken);
    }

    public async Task<Result<ApplicationDetailDto>> UpdatePayloadAsync(
        Actor actor, Guid id, UpdatePayloadRequest request, CancellationToken cancellationToken)
    {
        if (request.Payload.ValueKind != JsonValueKind.Object)
        {
            return Result<ApplicationDetailDto>.Failure(ApplicationErrors.InvalidPayload, "The form data must be a JSON object.");
        }

        var json = request.Payload.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
        {
            return Result<ApplicationDetailDto>.Failure(ApplicationErrors.InvalidPayload, "The form data is too large.");
        }

        var application = await db.Applications.Include(a => a.History).SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        if (application.CitizenId != actor.UserId || !actor.Has(Permissions.ApplicationSubmit))
        {
            return Result<ApplicationDetailDto>.Failure(ApplicationActorRules.NotApplicant, "Only the person who applied can do this.");
        }

        var workflow = BuiltInWorkflows.Find(application.WorkflowCode, application.WorkflowVersion);
        if (workflow is null)
        {
            return WorkflowMissing();
        }

        var updated = application.UpdatePayload(json, workflow);
        if (!updated.IsSuccess)
        {
            return Result<ApplicationDetailDto>.Failure(updated.Error!);
        }

        if (!await TrySaveAsync(cancellationToken))
        {
            return Conflict();
        }

        await AuditAsync(actor, "application.payload_updated", application.Id, cancellationToken);
        return await BuildDetailAsync(application, actor, cancellationToken);
    }

    public async Task<Result<ApplicationDetailDto>> ApplyTransitionAsync(
        Actor actor, Guid id, TransitionRequest request, CancellationToken cancellationToken)
    {
        var validation = await transitionValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ApplicationDetailDto>.Failure(Invalid(validation));
        }

        var application = await db.Applications.Include(a => a.History).SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        var workflow = BuiltInWorkflows.Find(application.WorkflowCode, application.WorkflowVersion);
        if (workflow is null)
        {
            return WorkflowMissing();
        }

        var now = timeProvider.GetUtcNow();
        var applied = application.ApplyAs(workflow, request.Transition.Trim(), actor, request.Note, now);
        if (!applied.IsSuccess)
        {
            return Result<ApplicationDetailDto>.Failure(applied.Error!);
        }

        // The reference number is issued the first time the application is submitted.
        if (application.ReferenceNumber is null && applied.Value.Transition == "submit")
        {
            var serviceCode = await db.Services.AsNoTracking()
                .Where(s => s.Id == application.ServiceId).Select(s => s.Code).FirstAsync(cancellationToken);
            application.AssignReference(
                ReferenceNumber.Format(serviceCode, now.UtcDateTime.Year, await NextReferenceAsync(cancellationToken)));
        }

        if (!await TrySaveAsync(cancellationToken))
        {
            return Conflict();
        }

        await AuditAsync(actor, $"application.{applied.Value.Transition}", application.Id, cancellationToken);
        return await BuildDetailAsync(application, actor, cancellationToken);
    }

    private async Task<Result<ApplicationDetailDto>> BuildDetailAsync(
        ServiceApplication application, Actor actor, CancellationToken cancellationToken)
    {
        var serviceCode = await db.Services.AsNoTracking()
            .Where(s => s.Id == application.ServiceId).Select(s => s.Code).FirstOrDefaultAsync(cancellationToken) ?? "UNKNOWN";
        var workflow = BuiltInWorkflows.Find(application.WorkflowCode, application.WorkflowVersion);
        return Result<ApplicationDetailDto>.Success(ToDetail(application, serviceCode, workflow, actor));
    }

    private static ApplicationDetailDto ToDetail(
        ServiceApplication application, string serviceCode, WorkflowDefinition? workflow, Actor actor)
    {
        using var payload = JsonDocument.Parse(application.PayloadJson);
        var editable = workflow is not null
            && workflow.EditableStates.Contains(application.State)
            && application.CitizenId == actor.UserId
            && actor.Has(Permissions.ApplicationSubmit);

        return new ApplicationDetailDto(
            application.Id,
            application.ReferenceNumber,
            serviceCode,
            application.State,
            application.CreatedAt,
            application.UpdatedAt,
            payload.RootElement.Clone(),
            editable,
            workflow is null ? [] : application.AvailableTransitions(workflow, actor),
            application.History
                .OrderBy(h => h.OccurredAt)
                .Select(h => new HistoryEntryDto(h.Transition, h.FromState, h.ToState, h.ActorId, h.Note, h.OccurredAt))
                .ToList());
    }

    private async Task<long> NextReferenceAsync(CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<long>($"SELECT nextval('application_reference_seq') AS \"Value\"").ToListAsync(cancellationToken)).Single();

    /// <summary>Saves, returning false when someone else changed the application first.</summary>
    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private Task AuditAsync(Actor actor, string action, Guid applicationId, CancellationToken cancellationToken) =>
        audit.AppendAsync(actor.UserId.ToString(), action, $"application:{applicationId}", null, cancellationToken);

    private static Error Invalid(ValidationResult validation) =>
        new(ApplicationErrors.InvalidRequest, string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));

    private static Result<ApplicationDetailDto> NotFound() =>
        Result<ApplicationDetailDto>.Failure(ApplicationErrors.NotFound, "Application not found.");

    private static Result<ApplicationDetailDto> Conflict() =>
        Result<ApplicationDetailDto>.Failure(ApplicationErrors.Conflict, "Someone else changed this application first. Reload and try again.");

    private static Result<ApplicationDetailDto> WorkflowMissing() =>
        Result<ApplicationDetailDto>.Failure(ApplicationErrors.WorkflowMissing, "The workflow for this application is not available.");
}
