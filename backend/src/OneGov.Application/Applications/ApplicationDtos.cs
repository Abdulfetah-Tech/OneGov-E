using System.Text.Json;
using FluentValidation;

namespace OneGov.Application.Applications;

public static class ApplicationErrors
{
    public const string NotFound = "application.not_found";
    public const string Forbidden = "application.forbidden";
    public const string ServiceNotFound = "application.service_not_found";
    public const string ServiceUnavailable = "application.service_unavailable";
    public const string InvalidRequest = "application.invalid_request";
    public const string InvalidPayload = "application.invalid_payload";
    public const string Conflict = "application.conflict";
    public const string WorkflowMissing = "application.workflow_missing";
}

public sealed record CreateApplicationRequest(string ServiceCode);

public sealed record UpdatePayloadRequest(JsonElement Payload);

public sealed record TransitionRequest(string Transition, string? Note = null);

public sealed record ListApplicationsQuery(string? State = null, int Page = 1, int PageSize = 20);

public sealed record ApplicationSummaryDto(
    Guid Id,
    string? ReferenceNumber,
    string ServiceCode,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record HistoryEntryDto(
    string Transition, string FromState, string ToState, Guid ActorId, string? Note, DateTimeOffset OccurredAt);

public sealed record ApplicationDetailDto(
    Guid Id,
    string? ReferenceNumber,
    string ServiceCode,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    JsonElement Payload,
    bool IsEditable,
    IReadOnlyList<string> AvailableTransitions,
    IReadOnlyList<HistoryEntryDto> History);

public sealed class CreateApplicationRequestValidator : AbstractValidator<CreateApplicationRequest>
{
    public CreateApplicationRequestValidator() => RuleFor(r => r.ServiceCode).NotEmpty().MaximumLength(64);
}

public sealed class TransitionRequestValidator : AbstractValidator<TransitionRequest>
{
    public TransitionRequestValidator()
    {
        RuleFor(r => r.Transition).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Note).MaximumLength(2000);
    }
}

public sealed class ListApplicationsQueryValidator : AbstractValidator<ListApplicationsQuery>
{
    public ListApplicationsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.State).MaximumLength(40);
    }
}
