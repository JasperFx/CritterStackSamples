namespace CritterCrush.Volunteering;

public class VolunteerApplicationsQueue
{
    public Guid Id { get; set; }
    public Guid ApplicantOwnerId { get; set; }
    public string AreasOfInterest { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}


// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
public class VolunteerApplicationsQueueProjection : SingleStreamProjection<VolunteerApplicationsQueue, Guid>
{
    public void Apply(VolunteerApplicationSubmitted submitted, VolunteerApplicationsQueue view)
    {
        view.ApplicantOwnerId = submitted.ApplicantOwnerId;
        view.AreasOfInterest = submitted.AreasOfInterest;
        view.Status = VolunteerApplicationStatus.Submitted;
    }

    public void Apply(VolunteerApplicationReviewed _, VolunteerApplicationsQueue view)
        => view.Status = VolunteerApplicationStatus.Reviewed;

    public void Apply(VolunteerApproved _, VolunteerApplicationsQueue view)
        => view.Status = VolunteerApplicationStatus.Approved;

    public void Apply(VolunteerApplicationRejected _, VolunteerApplicationsQueue view)
        => view.Status = VolunteerApplicationStatus.Rejected;
}

public static class GetVolunteerApplicationsQueueEndpoint
{
    [WolverineGet("/api/volunteerapplicationsqueue/{id}")]
    public static VolunteerApplicationsQueue Get([Entity(Required = true)] VolunteerApplicationsQueue volunteerApplicationsQueue) => volunteerApplicationsQueue;
}
