using OneGov.Domain.Applications;
using OneGov.Domain.Identity;
using OneGov.Domain.Notifications;
using OneGov.Domain.Workflow;

namespace OneGov.Domain.Tests;

public class NotificationTests
{
    private static readonly WorkflowDefinition Tax = BuiltInWorkflows.TaxFiling;
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    private static Actor Citizen(Guid id) => Actor.Create(id, [Roles.Citizen], mfaVerified: false);

    private static Actor Officer() => Actor.Create(Guid.NewGuid(), [Roles.Officer], mfaVerified: true);

    private static (ServiceApplication Application, Guid CitizenId) Submitted()
    {
        var citizenId = Guid.NewGuid();
        var application = ServiceApplication.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), citizenId, Tax);
        application.ApplyAs(Tax, "submit", Citizen(citizenId), null, Now);
        application.AssignReference("OG-TAXFILING-2026-000001");
        return (application, citizenId);
    }

    [Fact]
    public void The_applicant_gets_a_confirmation_when_they_submit()
    {
        var citizenId = Guid.NewGuid();
        var application = ServiceApplication.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), citizenId, Tax);
        var entry = application.ApplyAs(Tax, "submit", Citizen(citizenId), null, Now).Value;
        application.AssignReference("OG-TAXFILING-2026-000001");

        var notification = ApplicationNotifications.ForApplicant(application, entry, Now)!;

        Assert.Equal(citizenId, notification.UserId);
        Assert.Equal("submit", notification.Transition);
        Assert.Equal("submitted", notification.ToState);
        Assert.Equal("OG-TAXFILING-2026-000001", notification.ReferenceNumber);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void The_applicant_is_told_when_staff_act_and_sees_the_note()
    {
        var (application, citizenId) = Submitted();
        application.ApplyAs(Tax, "start_review", Officer(), null, Now.AddHours(1));
        var entry = application.ApplyAs(Tax, "request_documents", Officer(), "Please add the income statement.", Now.AddHours(2)).Value;

        var notification = ApplicationNotifications.ForApplicant(application, entry, Now.AddHours(2))!;

        Assert.Equal(citizenId, notification.UserId);
        Assert.Equal("documents_requested", notification.ToState);
        Assert.Equal("Please add the income statement.", notification.Note);
    }

    [Fact]
    public void The_applicant_is_not_told_about_their_own_other_actions()
    {
        var (application, citizenId) = Submitted();
        var entry = application.ApplyAs(Tax, "withdraw", Citizen(citizenId), null, Now.AddHours(1)).Value;

        Assert.Null(ApplicationNotifications.ForApplicant(application, entry, Now.AddHours(1)));
    }

    [Fact]
    public void Marking_read_keeps_the_first_read_time()
    {
        var (application, citizenId) = Submitted();
        var entry = application.History[0];
        var notification = ApplicationNotifications.ForApplicant(application, entry, Now)!;

        notification.MarkRead(Now.AddMinutes(5));
        notification.MarkRead(Now.AddMinutes(30));

        Assert.True(notification.IsRead);
        Assert.Equal(Now.AddMinutes(5), notification.ReadAt);
        Assert.Equal(citizenId, notification.UserId);
    }
}
