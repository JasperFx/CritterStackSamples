namespace CritterCrush.Scheduling;

/// <summary>
/// The states an appointment can be in. The curated format only knows
/// <c>string, Guid, bool, int, decimal, DateTimeOffset</c>, so the model spells this
/// <c>status: string</c> — the constants are here so a guard can name a state rather than quote one.
/// </summary>
public static class AppointmentStatus
{
    public const string Proposed = nameof(Proposed);
    public const string Confirmed = nameof(Confirmed);
    public const string Cancelled = nameof(Cancelled);
    public const string Completed = nameof(Completed);
    public const string NoShow = nameof(NoShow);
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

    /// <summary>Nothing more can happen to the appointment; the three ways it ends.</summary>
    public bool IsClosed =>
        Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed or AppointmentStatus.NoShow;

    public static Appointment Create(HomeCheckAppointmentProposed proposed) => new Appointment().With(proposed);

    public void Apply(HomeCheckAppointmentProposed e) => With(e);

    public void Apply(FosterHandoverAppointmentProposed e) => proposed(e.OwnerId, e.ShelterId, e.Kind, e.SourceId, e.ScheduledFor);

    public void Apply(SurrenderIntakeAppointmentProposed e) => proposed(e.OwnerId, e.ShelterId, e.Kind, e.SourceId, e.ScheduledFor);

    // If the event itself isn't used, use the C# "_" to erase the variable
    public void Apply(AppointmentConfirmed _) => Status = AppointmentStatus.Confirmed;

    public void Apply(AppointmentRescheduleRequested _) => RescheduleRequested = true;

    public void Apply(AppointmentRescheduled e)
    {
        ScheduledFor = e.ScheduledFor;

        // The request is spent. Leaving it set would let one request move the appointment
        // repeatedly, which is the "nobody asked to move this" guard passing on a stale yes.
        RescheduleRequested = false;
    }

    public void Apply(AppointmentCompleted _) => Status = AppointmentStatus.Completed;

    public void Apply(AppointmentCancelled _) => Status = AppointmentStatus.Cancelled;

    public void Apply(AppointmentNoShowRecorded _) => Status = AppointmentStatus.NoShow;

    internal Appointment With(HomeCheckAppointmentProposed e)
    {
        proposed(e.OwnerId, e.ShelterId, e.Kind, e.SourceId, e.ScheduledFor);
        return this;
    }

    private void proposed(Guid ownerId, Guid shelterId, string kind, Guid sourceId, DateTimeOffset scheduledFor)
    {
        OwnerId = ownerId;
        ShelterId = shelterId;
        Kind = kind;
        SourceId = sourceId;
        ScheduledFor = scheduledFor;
        Status = AppointmentStatus.Proposed;
    }
}
