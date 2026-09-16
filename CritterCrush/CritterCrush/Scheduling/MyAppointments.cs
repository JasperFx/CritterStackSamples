namespace CritterCrush.Scheduling;

public class MyAppointments
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public int AwaitingConfirmation { get; set; }
    public int Confirmed { get; set; }
    public int Closed { get; set; }
}

/// <summary>
/// One document per OWNER. An owner reached by two different flows — a home check and a surrender
/// intake — has two appointment streams and one page, which is the whole reason this is a
/// MultiStreamProjection rather than a snapshot of one stream.
///
/// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
/// </summary>
public class MyAppointmentsProjection : MultiStreamProjection<MyAppointments, Guid>
{
    public MyAppointmentsProjection()
    {
        Identity<HomeCheckAppointmentProposed>(x => x.OwnerId);
        Identity<FosterHandoverAppointmentProposed>(x => x.OwnerId);
        Identity<SurrenderIntakeAppointmentProposed>(x => x.OwnerId);
        Identity<AppointmentConfirmed>(x => x.OwnerId);
        Identity<AppointmentCompleted>(x => x.OwnerId);
        Identity<AppointmentCancelled>(x => x.OwnerId);
        Identity<AppointmentNoShowRecorded>(x => x.OwnerId);
    }

    public void Apply(HomeCheckAppointmentProposed _, MyAppointments view) => view.AwaitingConfirmation++;

    public void Apply(FosterHandoverAppointmentProposed _, MyAppointments view) => view.AwaitingConfirmation++;

    public void Apply(SurrenderIntakeAppointmentProposed _, MyAppointments view) => view.AwaitingConfirmation++;

    public void Apply(AppointmentConfirmed _, MyAppointments view)
    {
        view.AwaitingConfirmation--;
        view.Confirmed++;
    }

    public void Apply(AppointmentCompleted _, MyAppointments view) => closeFromConfirmed(view);

    public void Apply(AppointmentNoShowRecorded _, MyAppointments view) => closeFromConfirmed(view);

    public void Apply(AppointmentCancelled e, MyAppointments view)
    {
        if (e.WasConfirmed) view.Confirmed--;
        else view.AwaitingConfirmation--;
        view.Closed++;
    }

    private static void closeFromConfirmed(MyAppointments view)
    {
        view.Confirmed--;
        view.Closed++;
    }
}
