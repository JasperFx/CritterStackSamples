namespace CritterCrush.Scheduling;

/// <summary>
/// Routes by OwnerId: AppointmentCancelled, AppointmentCompleted, AppointmentConfirmed, AppointmentNoShowRecorded, FosterHandoverAppointmentProposed, HomeCheckAppointmentProposed, SurrenderIntakeAppointmentProposed.
/// </summary>
public interface IOwnerEvent
{
    Guid OwnerId { get; }
}


public class MyAppointments
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public int AwaitingConfirmation { get; set; }
    public int Confirmed { get; set; }
    public int Closed { get; set; }
}


// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
public class MyAppointmentsProjection : MultiStreamProjection<MyAppointments, Guid>
{
    public MyAppointmentsProjection()
    {
        // The slicing rule, without which this projection cannot be registered. One document
        // per key; Identities<T>(x => [x.OneId, x.OtherId]) where one event updates several.
        Identity<IOwnerEvent>(x => x.OwnerId);
    }


    public override MyAppointments Evolve(MyAppointments snapshot, Guid id, IEvent e)
    {
        snapshot ??= new MyAppointments { Id = id };

        // ONE place for anything derived from the identity — as Apply methods this was a line
        // at the top of every one of them, which is exactly where it goes missing.
        if (e.Data is IOwnerEvent routed) snapshot.OwnerId = routed.OwnerId;

        // Fill each arm in and delete the throw — the model's scenarios say what the view holds.
        switch (e.Data)
        {
            case HomeCheckAppointmentProposed:
                throw new NotImplementedException("TODO: MyAppointments — project HomeCheckAppointmentProposed");
            case FosterHandoverAppointmentProposed:
                throw new NotImplementedException("TODO: MyAppointments — project FosterHandoverAppointmentProposed");
            case SurrenderIntakeAppointmentProposed:
                throw new NotImplementedException("TODO: MyAppointments — project SurrenderIntakeAppointmentProposed");
            case AppointmentConfirmed:
                throw new NotImplementedException("TODO: MyAppointments — project AppointmentConfirmed");
            case AppointmentCompleted:
                throw new NotImplementedException("TODO: MyAppointments — project AppointmentCompleted");
            case AppointmentCancelled:
                throw new NotImplementedException("TODO: MyAppointments — project AppointmentCancelled");
            case AppointmentNoShowRecorded:
                throw new NotImplementedException("TODO: MyAppointments — project AppointmentNoShowRecorded");
        }


        return snapshot;
    }

}


public static class GetMyAppointmentsEndpoint
{
    [WolverineGet("/api/myappointments/{id}")]
    public static Task<MyAppointments?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<MyAppointments>(id, ct);
}


