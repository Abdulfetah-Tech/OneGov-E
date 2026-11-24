using OneGov.Application.Common;
using OneGov.Domain.Common;
using OneGov.Domain.Identity;

namespace OneGov.Application.Applications;

/// <summary>
/// Citizens' applications and their progress through a service's workflow. What a caller can see is already limited by their
/// data scope; what they can do is limited by their permissions and by the rules about who may act on an application.
/// </summary>
public interface IApplicationService
{
    Task<Result<ApplicationDetailDto>> CreateDraftAsync(Actor actor, CreateApplicationRequest request, CancellationToken cancellationToken);

    Task<Result<PagedResult<ApplicationSummaryDto>>> ListAsync(ListApplicationsQuery query, CancellationToken cancellationToken);

    Task<Result<ApplicationDetailDto>> GetAsync(Actor actor, Guid id, CancellationToken cancellationToken);

    Task<Result<ApplicationDetailDto>> UpdatePayloadAsync(
        Actor actor, Guid id, UpdatePayloadRequest request, CancellationToken cancellationToken);

    Task<Result<ApplicationDetailDto>> ApplyTransitionAsync(
        Actor actor, Guid id, TransitionRequest request, CancellationToken cancellationToken);
}
