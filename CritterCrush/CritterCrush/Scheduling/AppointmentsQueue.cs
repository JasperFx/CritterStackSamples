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


public static class GetAppointmentsQueueEndpoint
{
    [WolverineGet("/api/appointmentsqueue/{id}")]
    public static Task<AppointmentsQueue?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<AppointmentsQueue>(id, ct);
}


