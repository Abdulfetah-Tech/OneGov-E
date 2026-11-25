using OneGov.Application.Common;
using OneGov.Domain.Common;

namespace OneGov.Application.Notifications;

/// <summary>A person's own notifications. Nobody can read or change anyone else's.</summary>
public interface INotificationService
{
    Task<Result<PagedResult<NotificationDto>>> ListAsync(Guid userId, ListNotificationsQuery query, CancellationToken cancellationToken);

    Task<UnreadCountDto> UnreadCountAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<bool>> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);
}
