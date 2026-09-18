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

    public void Apply(VolunteerApplicationSubmitted volunteerApplicationSubmitted, VolunteerApplicationsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: VolunteerApplicationsQueue — project VolunteerApplicationSubmitted");
    }


    public void Apply(VolunteerApplicationReviewed volunteerApplicationReviewed, VolunteerApplicationsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: VolunteerApplicationsQueue — project VolunteerApplicationReviewed");
    }


    public void Apply(VolunteerApproved volunteerApproved, VolunteerApplicationsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: VolunteerApplicationsQueue — project VolunteerApproved");
    }


    public void Apply(VolunteerApplicationRejected volunteerApplicationRejected, VolunteerApplicationsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: VolunteerApplicationsQueue — project VolunteerApplicationRejected");
    }

}


public static class GetVolunteerApplicationsQueueEndpoint
{
    [WolverineGet("/api/volunteerapplicationsqueue/{id}")]
    public static Task<VolunteerApplicationsQueue?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<VolunteerApplicationsQueue>(id, ct);
}


