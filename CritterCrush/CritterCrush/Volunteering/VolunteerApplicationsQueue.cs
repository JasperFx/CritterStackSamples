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
    // Apply methods rather than one Evolve: every event folds DIFFERENTLY here, so a switch would
    // buy nothing and four one-line methods are easier to scan. The rule is about shared behaviour,
    // not about Evolve being better.

    public void Apply(VolunteerApplicationSubmitted e, VolunteerApplicationsQueue view)
    {
        view.ApplicantOwnerId = e.ApplicantOwnerId;
        view.AreasOfInterest = e.AreasOfInterest;
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
    public static Task<VolunteerApplicationsQueue?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<VolunteerApplicationsQueue>(id, ct);
}


