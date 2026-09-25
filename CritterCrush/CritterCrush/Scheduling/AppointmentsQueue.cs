namespace CritterCrush.Scheduling;

/// <summary>
/// Routes by ShelterId: AppointmentCancelled, AppointmentCompleted, AppointmentConfirmed, AppointmentNoShowRecorded, FosterHandoverAppointmentProposed, HomeCheckAppointmentProposed, SurrenderIntakeAppointmentProposed.
/// </summary>
public interface IShelterEvent
{
    Guid ShelterId { get; }
}


public class AppointmentsQueue
{
    public Guid Id { get; set; }
    public Guid ShelterId { get; set; }
    public int AwaitingConfirmation { get; set; }
    public int Confirmed { get; set; }
    public int Closed { get; set; }
}


// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
public class AppointmentsQueueProjection : MultiStreamProjection<AppointmentsQueue, Guid>
{
    public AppointmentsQueueProjection()
    {
        // The slicing rule, without which this projection cannot be registered. One document
        // per key; Identities<T>(x => [x.OneId, x.OtherId]) where one event updates several.
        Identity<IShelterEvent>(x => x.ShelterId);
    }


    public override AppointmentsQueue Evolve(AppointmentsQueue? snapshot, Guid id, IEvent e)
    {
        snapshot ??= new AppointmentsQueue { Id = id };

        // ONE place for anything derived from the identity — as Apply methods this was a line
        // at the top of every one of them, which is exactly where it goes missing.
        if (e.Data is IShelterEvent routed) snapshot.ShelterId = routed.ShelterId;

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


public static class GetAppointmentsQueueEndpoint
{
    [WolverineGet("/api/appointmentsqueue/{id}")]
    public static AppointmentsQueue Get([Entity(Required = true)] AppointmentsQueue appointmentsQueue) => appointmentsQueue;
}
