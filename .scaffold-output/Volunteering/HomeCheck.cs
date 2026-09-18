namespace CritterCrush.Volunteering;

public class HomeCheck
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ShelterId { get; set; }
    public Guid VolunteerOwnerId { get; set; }
    public string Status { get; set; } = string.Empty;

    public static HomeCheck Create(HomeCheckRequested homeCheckRequested)
    {
        // TODO: fold the creating event into the initial state
        return new HomeCheck();
    }


    public void Apply(HomeCheckRequested homeCheckRequested)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(HomeCheckAssignmentAccepted homeCheckAssignmentAccepted)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(HomeCheckReportSubmitted homeCheckReportSubmitted)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }

}


