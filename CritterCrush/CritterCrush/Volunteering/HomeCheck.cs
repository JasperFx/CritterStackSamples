namespace CritterCrush.Volunteering;

/// <inheritdoc cref="CritterCrush.Scheduling.AppointmentStatus"/>
public static class HomeCheckStatus
{
    public const string Requested = nameof(Requested);
    public const string Accepted = nameof(Accepted);
    public const string Reported = nameof(Reported);
}

public class HomeCheck
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ShelterId { get; set; }
    public Guid VolunteerOwnerId { get; set; }
    public string Status { get; set; } = string.Empty;

    public static HomeCheck Create(HomeCheckRequested e) => new()
    {
        ApplicationId = e.ApplicationId,
        OwnerId = e.OwnerId,
        ShelterId = e.ShelterId,
        Status = HomeCheckStatus.Requested
    };

    public void Apply(HomeCheckRequested e)
    {
        ApplicationId = e.ApplicationId;
        OwnerId = e.OwnerId;
        ShelterId = e.ShelterId;
        Status = HomeCheckStatus.Requested;
    }

    public void Apply(HomeCheckAssignmentAccepted e)
    {
        VolunteerOwnerId = e.VolunteerOwnerId;
        Status = HomeCheckStatus.Accepted;
    }

    public void Apply(HomeCheckReportSubmitted _) => Status = HomeCheckStatus.Reported;
}
