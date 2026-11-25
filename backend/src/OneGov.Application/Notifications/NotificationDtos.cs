using FluentValidation;

namespace OneGov.Application.Notifications;

public static class NotificationErrors
{
    public const string NotFound = "notification.not_found";
    public const string InvalidRequest = "notification.invalid_request";
}

public sealed record ListNotificationsQuery(bool UnreadOnly = false, int Page = 1, int PageSize = 20);

public sealed record NotificationDto(
    Guid Id,
    Guid ApplicationId,
    string? ReferenceNumber,
    string Transition,
    string ToState,
    string? Note,
    DateTimeOffset CreatedAt,
    bool IsRead);

public sealed record UnreadCountDto(int Count);

public sealed class ListNotificationsQueryValidator : AbstractValidator<ListNotificationsQuery>
{
    public ListNotificationsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
