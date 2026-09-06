namespace CritterCrush.Appointments;

public class MyAppointments
{
    public Guid Id { get; set; }
    // TODO: the projected columns the model's scenarios assert on
}


// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
public class MyAppointmentsProjection : MultiStreamProjection<MyAppointments, Guid>
{
    public MyAppointmentsProjection()
    {
        // TODO: the fan-out routing — Identities<SourceEvent>(x => [x.OneId, x.OtherId]);
    }


    // TODO: Apply methods per source event
}


public static class GetMyAppointmentsEndpoint
{
    [WolverineGet("/api/myappointments/{id}")]
    public static Task<MyAppointments?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<MyAppointments>(id, ct);
}


