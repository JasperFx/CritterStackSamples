namespace CritterCrush.Appointments;

public class AppointmentsQueue
{
    public Guid Id { get; set; }
    // TODO: the projected columns the model's scenarios assert on
}


// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
public class AppointmentsQueueProjection : MultiStreamProjection<AppointmentsQueue, Guid>
{
    public AppointmentsQueueProjection()
    {
        // TODO: the fan-out routing — Identities<SourceEvent>(x => [x.OneId, x.OtherId]);
    }


    // TODO: Apply methods per source event
}


public static class GetAppointmentsQueueEndpoint
{
    [WolverineGet("/api/appointmentsqueue/{id}")]
    public static Task<AppointmentsQueue?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<AppointmentsQueue>(id, ct);
}


