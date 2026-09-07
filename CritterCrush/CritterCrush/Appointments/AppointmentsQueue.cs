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


    public void Apply(HomeCheckAppointmentProposed e, AppointmentsQueue view)
    {
        // TODO: project this event onto the view — the model's scenarios say what it must contain
        throw new NotImplementedException("TODO: AppointmentsQueue — project HomeCheckAppointmentProposed");
    }
}


public static class GetAppointmentsQueueEndpoint
{
    [WolverineGet("/api/appointmentsqueue/{id}")]
    public static Task<AppointmentsQueue?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<AppointmentsQueue>(id, ct);
}


