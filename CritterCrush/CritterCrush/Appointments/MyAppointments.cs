namespace CritterCrush.Appointments;

public class MyAppointments
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public int AwaitingConfirmation { get; set; }
    public int Confirmed { get; set; }
    public DateTimeOffset NextAppointmentAt { get; set; }
}


/// <summary>
/// What one owner has coming up, across every appointment of every dog of theirs — the model's
/// fan-out slice, and the reason every appointment event carries <c>OwnerId</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keyed by the OWNER, not by the stream.</b> That is the whole difference from
/// <see cref="AppointmentsQueueProjection"/> beside it: the queue is one row per appointment and
/// folds its own stream, while this folds many streams into one document per owner. It is what
/// makes the slice worth having and what made it unspecifiable until bobcat#236 gave the assertion
/// step an id — a spec can now say "the MyAppointments read model with id &lt;owner&gt;" instead of
/// being forced to ask about the stream it happened to arrange.
/// </para>
/// <para>
/// <b>NextAppointmentAt is the earliest time this owner is still expected somewhere.</b> Counts are
/// exact; this one is honest rather than precise, and the limit is worth stating: the document
/// holds no per-appointment state, so when an appointment leaves (completed, cancelled, no-show) or
/// moves later, the earliest time cannot be RECOMPUTED from what is stored. It therefore only ever
/// moves earlier, and a reschedule that pushes the only appointment later leaves the old time
/// standing. Tracking it properly means carrying the times themselves, which the declared model
/// does not — so this is a modelling decision, and the model is where it should change.
/// </para>
/// </remarks>
// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
public class MyAppointmentsProjection : MultiStreamProjection<MyAppointments, Guid>
{
    public MyAppointmentsProjection()
    {
        // The slicing rule, without which this projection cannot be registered — and the line that
        // decides what this document IS. The scaffold routed every event by AppointmentId, which
        // would have produced one document per appointment: the queue again, under another name.
        Identity<HomeCheckAppointmentProposed>(x => x.OwnerId);
        Identity<FosterHandoverAppointmentProposed>(x => x.OwnerId);
        Identity<SurrenderIntakeAppointmentProposed>(x => x.OwnerId);
        Identity<AppointmentConfirmed>(x => x.OwnerId);
        Identity<RescheduleRequested>(x => x.OwnerId);
        Identity<AppointmentRescheduled>(x => x.OwnerId);
        Identity<AppointmentCompleted>(x => x.OwnerId);
        Identity<AppointmentCancelled>(x => x.OwnerId);
        Identity<AppointmentNoShowRecorded>(x => x.OwnerId);
    }

    public void Apply(HomeCheckAppointmentProposed e, MyAppointments view) => proposed(view, e.OwnerId, e.ProposedFor);

    public void Apply(FosterHandoverAppointmentProposed e, MyAppointments view) => proposed(view, e.OwnerId, e.ProposedFor);

    public void Apply(SurrenderIntakeAppointmentProposed e, MyAppointments view) => proposed(view, e.OwnerId, e.ProposedFor);

    public void Apply(AppointmentConfirmed e, MyAppointments view)
    {
        view.OwnerId = e.OwnerId;
        if (view.AwaitingConfirmation > 0) view.AwaitingConfirmation--;
        view.Confirmed++;
    }

    /// <summary>
    /// Asking for a different time does not move the appointment — the shelter has not agreed to
    /// anything yet — so nothing here changes until <see cref="AppointmentRescheduled"/> arrives.
    /// </summary>
    public void Apply(RescheduleRequested e, MyAppointments view) => view.OwnerId = e.OwnerId;

    public void Apply(AppointmentRescheduled e, MyAppointments view) => sooner(view, e.OwnerId, e.ScheduledFor);

    public void Apply(AppointmentCompleted e, MyAppointments view) => resolved(view, e.OwnerId);

    public void Apply(AppointmentCancelled e, MyAppointments view) => resolved(view, e.OwnerId);

    public void Apply(AppointmentNoShowRecorded e, MyAppointments view) => resolved(view, e.OwnerId);

    private static void proposed(MyAppointments view, Guid ownerId, DateTimeOffset at)
    {
        view.AwaitingConfirmation++;
        sooner(view, ownerId, at);
    }

    /// <summary>
    /// An appointment that will not happen — completed, cancelled or missed. It leaves whichever
    /// count it was in; a cancellation before confirmation takes one off the waiting list rather
    /// than off the confirmed one.
    /// </summary>
    private static void resolved(MyAppointments view, Guid ownerId)
    {
        view.OwnerId = ownerId;
        if (view.Confirmed > 0) view.Confirmed--;
        else if (view.AwaitingConfirmation > 0) view.AwaitingConfirmation--;
    }

    private static void sooner(MyAppointments view, Guid ownerId, DateTimeOffset at)
    {
        view.OwnerId = ownerId;
        if (view.NextAppointmentAt == default || at < view.NextAppointmentAt) view.NextAppointmentAt = at;
    }
}


public static class GetMyAppointmentsEndpoint
{
    [WolverineGet("/api/myappointments/{id}")]
    public static Task<MyAppointments?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<MyAppointments>(id, ct);
}


