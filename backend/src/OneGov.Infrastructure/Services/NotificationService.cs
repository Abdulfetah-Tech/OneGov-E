using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OneGov.Application.Common;
using OneGov.Application.Notifications;
using OneGov.Domain.Common;
using OneGov.Infrastructure.Persistence;

namespace OneGov.Infrastructure.Services;

public sealed class NotificationService(
    OneGovDbContext db, TimeProvider timeProvider, IValidator<ListNotificationsQuery> listValidator) : INotificationService
{
    public async Task<Result<PagedResult<NotificationDto>>> ListAsync(
        Guid userId, ListNotificationsQuery query, CancellationToken cancellationToken)
    {
        var validation = await listValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<PagedResult<NotificationDto>>.Failure(
                NotificationErrors.InvalidRequest, string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (query.UnreadOnly)
        {
            mine = mine.Where(n => n.ReadAt == null);
        }

        var rows = mine.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => new NotificationDto(
                n.Id, n.ApplicationId, n.ReferenceNumber, n.Transition, n.ToState, n.Note, n.CreatedAt, n.ReadAt != null))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<NotificationDto>>.Success(new PagedResult<NotificationDto>(items, query.Page, query.PageSize, total));
    }

    public async Task<UnreadCountDto> UnreadCountAsync(Guid userId, CancellationToken cancellationToken) =>
        new(await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken));

    public async Task<Result<bool>> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
        if (notification is null)
        {
            return Result<bool>.Failure(NotificationErrors.NotFound, "Notification not found.");
        }

        notification.MarkRead(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    public Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = (DateTimeOffset?)timeProvider.GetUtcNow();
        return db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), cancellationToken);
    }
}
