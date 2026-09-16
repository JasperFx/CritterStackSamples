namespace CritterCrush.Scheduling;

/// <summary>
/// The lifecycle an appointment moves through. Strings rather than an enum because the model
/// declares `status: string` — the curated file is the source, and an enum here would be a shape
/// the model never asked for.
/// </summary>
public static class AppointmentStatus
{
    public const string Proposed = nameof(Proposed);
    public const string Confirmed = nameof(Confirmed);
    public const string Completed = nameof(Completed);
    public const string Cancelled = nameof(Cancelled);
    public const string NoShow = nameof(NoShow);
}

/// <summary>
/// Which flow asked for the visit. The board's own words: "a single generic, purpose-tagged
/// Appointment concept (HomeCheck | FosterHandover | SurrenderIntake, linking back to the source
/// entity id) reused across three automation entry points rather than three separate booking flows".
/// </summary>
public static class AppointmentKind
{
    public const string HomeCheck = nameof(HomeCheck);
    public const string FosterHandover = nameof(FosterHandover);
    public const string SurrenderIntake = nameof(SurrenderIntake);
}

public class Appointment
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ShelterId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool RescheduleRequested { get; set; }

    /// <summary>
    /// Closed exactly once, by whichever of the three closing events got there. The guards on
    /// Complete, Cancel and RecordNoShow all read this, which is why it lives here and not in
    /// three copies of the same boolean expression.
    /// </summary>
    public bool IsClosed =>
        Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow;

    // Three events can start the stream — one per entry point — and they carry the same shape, so
    // each Create funnels into one place.
    public static Appointment Create(HomeCheckAppointmentProposed e) => proposed(e.OwnerId, e.ShelterId, e.Kind, e.SourceId, e.ScheduledFor);

    public static Appointment Create(FosterHandoverAppointmentProposed e) => proposed(e.OwnerId, e.ShelterId, e.Kind, e.SourceId, e.ScheduledFor);

    public static Appointment Create(SurrenderIntakeAppointmentProposed e) => proposed(e.OwnerId, e.ShelterId, e.Kind, e.SourceId, e.ScheduledFor);

    public void Apply(AppointmentConfirmed _) => Status = AppointmentStatus.Confirmed;

    public void Apply(AppointmentRescheduleRequested _) => RescheduleRequested = true;

    // A move satisfies the request that asked for it, and leaves the appointment confirmed — the
    // counterparty already agreed to come, just not at the old time.
    public void Apply(AppointmentRescheduled e)
    {
        ScheduledFor = e.ScheduledFor;
        RescheduleRequested = false;
    }

    public void Apply(AppointmentCompleted _) => Status = AppointmentStatus.Completed;

    public void Apply(AppointmentCancelled _) => Status = AppointmentStatus.Cancelled;

    public void Apply(AppointmentNoShowRecorded _) => Status = AppointmentStatus.NoShow;

    private static Appointment proposed(Guid ownerId, Guid shelterId, string kind, Guid sourceId, DateTimeOffset scheduledFor) =>
        new()
        {
            OwnerId = ownerId,
            ShelterId = shelterId,
            Kind = kind,
            SourceId = sourceId,
            ScheduledFor = scheduledFor,
            Status = AppointmentStatus.Proposed
        };
}
