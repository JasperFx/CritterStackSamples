namespace CritterCrush.Volunteering;

public class VolunteerApplicationsQueue
{
    public Guid Id { get; set; }
    public Guid ApplicantOwnerId { get; set; }
    public string AreasOfInterest { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// The other view shape, deliberately: SINGLE-stream, one document per application, folded from
/// that application's own stream. No identity rules, because the stream id IS the document id —
/// the contrast with BookingAppointments' two multi-stream views is the point of having both.
///
/// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
/// </summary>
public class VolunteerApplicationsQueueProjection : SingleStreamProjection<VolunteerApplicationsQueue, Guid>
{
    public void Apply(VolunteerApplicationSubmitted e, VolunteerApplicationsQueue view)
    {
        view.ApplicantOwnerId = e.ApplicantOwnerId;
        view.AreasOfInterest = e.AreasOfInterest;
        view.Status = VolunteerApplicationStatus.Submitted;
    }

    public void Apply(VolunteerApplicationReviewed _, VolunteerApplicationsQueue view) =>
        view.Status = VolunteerApplicationStatus.Reviewed;

    public void Apply(VolunteerApproved _, VolunteerApplicationsQueue view) =>
        view.Status = VolunteerApplicationStatus.Approved;

    public void Apply(VolunteerApplicationRejected _, VolunteerApplicationsQueue view) =>
        view.Status = VolunteerApplicationStatus.Rejected;
}
