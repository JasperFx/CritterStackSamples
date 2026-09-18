namespace CritterCrush.Volunteering;

public class VolunteerApplication
{
    public Guid Id { get; set; }
    public Guid ApplicantOwnerId { get; set; }
    public string AreasOfInterest { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public static VolunteerApplication Create(VolunteerApplicationSubmitted volunteerApplicationSubmitted)
    {
        // TODO: fold the creating event into the initial state
        return new VolunteerApplication();
    }


    public void Apply(VolunteerApplicationReviewed volunteerApplicationReviewed)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(VolunteerApproved volunteerApproved)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(VolunteerApplicationRejected volunteerApplicationRejected)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }

}


