namespace CritterCrush.Scheduling;

public class AppointmentsQueue
{
    public Guid Id { get; set; }
    public Guid ShelterId { get; set; }
    public int AwaitingConfirmation { get; set; }
    public int Confirmed { get; set; }
    public int Closed { get; set; }
}

/// <summary>
/// One document per SHELTER, folding every appointment stream that shelter owns. Multi-stream
/// because the identity is the shelter, not the stream — a shelter with nine appointments has nine
/// streams and one queue.
///
/// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
/// </summary>
public class AppointmentsQueueProjection : MultiStreamProjection<AppointmentsQueue, Guid>
{
    public AppointmentsQueueProjection()
    {
        Identity<HomeCheckAppointmentProposed>(x => x.ShelterId);
        Identity<FosterHandoverAppointmentProposed>(x => x.ShelterId);
        Identity<SurrenderIntakeAppointmentProposed>(x => x.ShelterId);
        Identity<AppointmentConfirmed>(x => x.ShelterId);
        Identity<AppointmentCompleted>(x => x.ShelterId);
        Identity<AppointmentCancelled>(x => x.ShelterId);
        Identity<AppointmentNoShowRecorded>(x => x.ShelterId);
    }

    public void Apply(HomeCheckAppointmentProposed _, AppointmentsQueue view) => view.AwaitingConfirmation++;

    public void Apply(FosterHandoverAppointmentProposed _, AppointmentsQueue view) => view.AwaitingConfirmation++;

    public void Apply(SurrenderIntakeAppointmentProposed _, AppointmentsQueue view) => view.AwaitingConfirmation++;

    public void Apply(AppointmentConfirmed _, AppointmentsQueue view)
    {
        view.AwaitingConfirmation--;
        view.Confirmed++;
    }

    public void Apply(AppointmentCompleted _, AppointmentsQueue view) => closeFromConfirmed(view);

    public void Apply(AppointmentNoShowRecorded _, AppointmentsQueue view) => closeFromConfirmed(view);

    // The only closing event that can arrive from either bucket, which is why it carries the answer.
    public void Apply(AppointmentCancelled e, AppointmentsQueue view)
    {
        if (e.WasConfirmed) view.Confirmed--;
        else view.AwaitingConfirmation--;
        view.Closed++;
    }

    private static void closeFromConfirmed(AppointmentsQueue view)
    {
        view.Confirmed--;
        view.Closed++;
    }
}
