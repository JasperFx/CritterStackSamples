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


    public override AppointmentsQueue Evolve(AppointmentsQueue snapshot, Guid id, IEvent e)
    {
        snapshot ??= new AppointmentsQueue { Id = id };

        // ONE place for anything derived from the identity — as Apply methods this was a line
        // at the top of every one of them, which is exactly where it goes missing.
        if (e.Data is IShelterEvent routed) snapshot.ShelterId = routed.ShelterId;

        // Fill each arm in and delete the throw — the model's scenarios say what the view holds.
        switch (e.Data)
        {
            case HomeCheckAppointmentProposed:
                throw new NotImplementedException("TODO: AppointmentsQueue — project HomeCheckAppointmentProposed");
            case FosterHandoverAppointmentProposed:
                throw new NotImplementedException("TODO: AppointmentsQueue — project FosterHandoverAppointmentProposed");
            case SurrenderIntakeAppointmentProposed:
                throw new NotImplementedException("TODO: AppointmentsQueue — project SurrenderIntakeAppointmentProposed");
            case AppointmentConfirmed:
                throw new NotImplementedException("TODO: AppointmentsQueue — project AppointmentConfirmed");
            case AppointmentCompleted:
                throw new NotImplementedException("TODO: AppointmentsQueue — project AppointmentCompleted");
            case AppointmentCancelled:
                throw new NotImplementedException("TODO: AppointmentsQueue — project AppointmentCancelled");
            case AppointmentNoShowRecorded:
                throw new NotImplementedException("TODO: AppointmentsQueue — project AppointmentNoShowRecorded");
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


