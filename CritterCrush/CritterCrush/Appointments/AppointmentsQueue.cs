namespace CritterCrush.Appointments;

public class AppointmentsQueue
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ShelterId { get; set; }
    public Guid DogId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ScheduledFor { get; set; }
    public bool AwaitingAction { get; set; }
}


/// <summary>
/// The shelter's work queue: one row per appointment, keyed by the appointment's own stream, so
/// this is a single-stream fold. A row is "awaiting action" for as long as the appointment is still
/// live — proposed, confirmed, or in the middle of being rescheduled — and stops the moment the
/// stream reaches a terminal state (completed, cancelled, no-show).
/// </summary>
// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
public class AppointmentsQueueProjection : SingleStreamProjection<AppointmentsQueue, Guid>
{
    public static AppointmentsQueue Create(HomeCheckAppointmentProposed e) =>
        proposed(e.AppointmentId, e.OwnerId, e.ShelterId, e.DogId, AppointmentKinds.HomeCheck, e.ProposedFor);

    public static AppointmentsQueue Create(FosterHandoverAppointmentProposed e) =>
        proposed(e.AppointmentId, e.OwnerId, e.ShelterId, e.DogId, AppointmentKinds.FosterHandover, e.ProposedFor);

    public static AppointmentsQueue Create(SurrenderIntakeAppointmentProposed e) =>
        proposed(e.AppointmentId, e.OwnerId, e.ShelterId, e.DogId, AppointmentKinds.SurrenderIntake, e.ProposedFor);

    public void Apply(AppointmentConfirmed appointmentConfirmed, AppointmentsQueue view)
    {
        view.Status = AppointmentStatuses.Confirmed;
        view.AwaitingAction = true;
    }

    public void Apply(RescheduleRequested rescheduleRequested, AppointmentsQueue view)
    {
        // The owner asked for a new time; the slot stays as proposed until the shelter moves it.
        view.Status = AppointmentStatuses.RescheduleRequested;
        view.AwaitingAction = true;
    }

    public void Apply(AppointmentRescheduled appointmentRescheduled, AppointmentsQueue view)
    {
        view.ScheduledFor = appointmentRescheduled.ScheduledFor;
        view.Status = AppointmentStatuses.Rescheduled;
        view.AwaitingAction = true;
    }

    public void Apply(AppointmentCompleted appointmentCompleted, AppointmentsQueue view)
    {
        view.Status = AppointmentStatuses.Completed;
        view.AwaitingAction = false;
    }

    public void Apply(AppointmentCancelled appointmentCancelled, AppointmentsQueue view)
    {
        view.Status = AppointmentStatuses.Cancelled;
        view.AwaitingAction = false;
    }

    public void Apply(AppointmentNoShowRecorded appointmentNoShowRecorded, AppointmentsQueue view)
    {
        view.Status = AppointmentStatuses.NoShow;
        view.AwaitingAction = false;
    }

    private static AppointmentsQueue proposed(Guid appointmentId, Guid ownerId, Guid shelterId, Guid dogId,
        string kind, DateTimeOffset proposedFor) => new()
    {
        Id = appointmentId,
        OwnerId = ownerId,
        ShelterId = shelterId,
        DogId = dogId,
        Kind = kind,
        Status = AppointmentStatuses.Proposed,
        ScheduledFor = proposedFor,
        AwaitingAction = true
    };
}

/// <summary>The three kinds of appointment the shelter books, as the queue names them.</summary>
public static class AppointmentKinds
{
    public const string HomeCheck = "HomeCheck";
    public const string FosterHandover = "FosterHandover";
    public const string SurrenderIntake = "SurrenderIntake";
}

/// <summary>Where an appointment is in its life, as the queue names it.</summary>
public static class AppointmentStatuses
{
    public const string Proposed = "Proposed";
    public const string Confirmed = "Confirmed";
    public const string RescheduleRequested = "RescheduleRequested";
    public const string Rescheduled = "Rescheduled";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "NoShow";
}


public static class GetAppointmentsQueueEndpoint
{
    [WolverineGet("/api/appointmentsqueue/{id}")]
    public static Task<AppointmentsQueue?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<AppointmentsQueue>(id, ct);
}
