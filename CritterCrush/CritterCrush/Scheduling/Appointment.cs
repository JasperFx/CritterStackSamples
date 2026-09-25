namespace CritterCrush.Scheduling;

/// <summary>
/// Where an appointment is in its life. <c>string</c> rather than an enum because the curated
/// format knows only string, Guid, bool, int, decimal and DateTimeOffset — so the model states
/// these as strings and the domain follows it rather than diverging from the design record.
/// </summary>
public static class AppointmentStatus
{
    public const string Proposed = nameof(Proposed);
    public const string Confirmed = nameof(Confirmed);
    public const string Completed = nameof(Completed);
    public const string Cancelled = nameof(Cancelled);
    public const string NoShow = nameof(NoShow);

    /// <summary>Nothing more can happen to it. The three guards that say "already closed" mean this.</summary>
    public static bool IsClosed(string status) => status is Completed or Cancelled or NoShow;
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

    public static Appointment Create(HomeCheckAppointmentProposed proposed) => new()
    {
        OwnerId = proposed.OwnerId,
        ShelterId = proposed.ShelterId,
        Kind = proposed.Kind,
        SourceId = proposed.SourceId,
        ScheduledFor = proposed.ScheduledFor,
        Status = AppointmentStatus.Proposed
    };

    // The other two proposals create the same appointment from a different origin. They are Apply
    // rather than Create because Marten picks ONE creating event per aggregate, and the home check
    // is the one the model draws first.
    public void Apply(FosterHandoverAppointmentProposed proposed) => proposedAs(
        proposed.OwnerId, proposed.ShelterId, proposed.Kind, proposed.SourceId, proposed.ScheduledFor);

    public void Apply(SurrenderIntakeAppointmentProposed proposed) => proposedAs(
        proposed.OwnerId, proposed.ShelterId, proposed.Kind, proposed.SourceId, proposed.ScheduledFor);

    public void Apply(AppointmentConfirmed _) => Status = AppointmentStatus.Confirmed;

    public void Apply(AppointmentRescheduleRequested _) => RescheduleRequested = true;

    public void Apply(AppointmentRescheduled rescheduled)
    {
        ScheduledFor = rescheduled.ScheduledFor;

        // The request is answered by the move, so a second move needs a second request. Leaving
        // this set would let one request authorise every future reschedule.
        RescheduleRequested = false;
    }

    public void Apply(AppointmentCompleted _) => Status = AppointmentStatus.Completed;

    public void Apply(AppointmentCancelled _) => Status = AppointmentStatus.Cancelled;

    public void Apply(AppointmentNoShowRecorded _) => Status = AppointmentStatus.NoShow;

    private void proposedAs(Guid ownerId, Guid shelterId, string kind, Guid sourceId, DateTimeOffset scheduledFor)
    {
        OwnerId = ownerId;
        ShelterId = shelterId;
        Kind = kind;
        SourceId = sourceId;
        ScheduledFor = scheduledFor;
        Status = AppointmentStatus.Proposed;
    }
}
