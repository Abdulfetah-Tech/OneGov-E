using OneGov.Domain.Applications;
using OneGov.Domain.Common;

namespace OneGov.Domain.Notifications;

/// <summary>
/// A message for one person about something that happened to their application. It stores facts, not text, so the web app
/// can show it in the reader's language. It is written in the same database transaction as the change it describes, so a
/// notification can neither be lost nor describe something that did not happen.
/// </summary>
public sealed class Notification : Entity
{
    private Notification()
    {
    }

    public Guid UserId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string? ReferenceNumber { get; private set; }

    public string Transition { get; private set; } = string.Empty;

    public string ToState { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid userId, ServiceApplication application, ApplicationHistoryEntry entry, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            ApplicationId = application.Id,
            ReferenceNumber = application.ReferenceNumber,
            Transition = entry.Transition,
            ToState = entry.ToState,
            Note = entry.Note,
            CreatedAt = now,
        };

    /// <summary>Marking an already read notification keeps its original time.</summary>
    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}

/// <summary>Decides who is told about a step. Pure, no I/O.</summary>
public static class ApplicationNotifications
{
    /// <summary>
    /// The applicant is told when someone else acts on their application, and gets a confirmation when they submit it.
    /// They are not told about their own other actions, which they just did.
    /// </summary>
    public static Notification? ForApplicant(ServiceApplication application, ApplicationHistoryEntry entry, DateTimeOffset now)
    {
        var doneByApplicant = entry.ActorId == application.CitizenId;
        if (doneByApplicant && entry.Transition != "submit")
        {
            return null;
        }

        return Notification.Create(application.CitizenId, application, entry, now);
    }
}
