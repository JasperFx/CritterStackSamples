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


    public override MyAppointments Evolve(MyAppointments? snapshot, Guid id, IEvent e)
    {
        snapshot ??= new MyAppointments { Id = id };

        // ONE place for anything derived from the identity — as Apply methods this was a line
        // at the top of every one of them, which is exactly where it goes missing.
        if (e.Data is IOwnerEvent routed) snapshot.OwnerId = routed.OwnerId;

        switch (e.Data)
        {
            // Three chapters propose appointments and all three land in the same bucket, so the
            // labels stack rather than repeating one body three times.
            case HomeCheckAppointmentProposed:
            case FosterHandoverAppointmentProposed:
            case SurrenderIntakeAppointmentProposed:
                snapshot.AwaitingConfirmation++;
                break;

            case AppointmentConfirmed:
                snapshot.AwaitingConfirmation--;
                snapshot.Confirmed++;
                break;

            // A closing event has to know which bucket the appointment was in, which is why
            // AppointmentCancelled carries WasConfirmed: after the fold nothing can say.
            case AppointmentCancelled cancelled:
                if (cancelled.WasConfirmed) snapshot.Confirmed--;
                else snapshot.AwaitingConfirmation--;
                snapshot.Closed++;
                break;

            // Completion and a no-show are both reachable only from Confirmed, so both leave that
            // bucket. The guards are what make that true.
            case AppointmentCompleted:
            case AppointmentNoShowRecorded:
                snapshot.Confirmed--;
                snapshot.Closed++;
                break;
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


