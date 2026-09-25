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
            // A proposal joins the queue waiting on the counterparty.
            case HomeCheckAppointmentProposed:
            case FosterHandoverAppointmentProposed:
            case SurrenderIntakeAppointmentProposed:
                snapshot.AwaitingConfirmation++;
                break;

            case AppointmentConfirmed:
                snapshot.AwaitingConfirmation--;
                snapshot.Confirmed++;
                break;

            // Completing and no-showing both end a CONFIRMED appointment — the guards on those two
            // slices refuse anything else, so there is no other bucket they can be leaving.
            case AppointmentCompleted:
            case AppointmentNoShowRecorded:
                snapshot.Confirmed--;
                snapshot.Closed++;
                break;

            // Cancelling is the one that can arrive from either bucket, which is the whole reason
            // WasConfirmed rides on the event: a projection has no prior state to consult, and
            // guessing here is how a counter goes negative and stays there.
            case AppointmentCancelled cancelled:
                if (cancelled.WasConfirmed) snapshot.Confirmed--;
                else snapshot.AwaitingConfirmation--;
                snapshot.Closed++;
                break;
        }


        return snapshot;
    }

}


public static class GetMyAppointmentsEndpoint
{
    [WolverineGet("/api/myappointments/{id}")]
    public static MyAppointments Get([Entity(Required = true)] MyAppointments myAppointments) => myAppointments;
}
