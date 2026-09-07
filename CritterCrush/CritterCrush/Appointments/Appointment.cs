namespace CritterCrush.Appointments;

/// <summary>
/// The status vocabulary the Appointment stream moves through. Strings rather than an enum so the
/// read models and the scenarios' tables share one spelling.
/// </summary>
public static class AppointmentStatus
{
    public const string Proposed = "Proposed";
    public const string Confirmed = "Confirmed";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "NoShow";
}

public class Appointment
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ShelterId { get; set; }
    public Guid DogId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ScheduledFor { get; set; }
    public bool RescheduleRequested { get; set; }

    public static Appointment Create(HomeCheckAppointmentProposed homeCheckAppointmentProposed)
    {
        var appointment = new Appointment();
        appointment.Apply(homeCheckAppointmentProposed);
        return appointment;
    }


    public void Apply(HomeCheckAppointmentProposed homeCheckAppointmentProposed)
    {
        // The stream id is the appointment id; the store assigns Id from the stream, so a scenario
        // that arranges this event partially (no appointmentId) still folds correctly.
        OwnerId = homeCheckAppointmentProposed.OwnerId;
        ShelterId = homeCheckAppointmentProposed.ShelterId;
        DogId = homeCheckAppointmentProposed.DogId;
        Kind = "HomeCheck";
        Status = AppointmentStatus.Proposed;
        ScheduledFor = homeCheckAppointmentProposed.ProposedFor;
    }


    public void Apply(FosterHandoverAppointmentProposed fosterHandoverAppointmentProposed)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(SurrenderIntakeAppointmentProposed surrenderIntakeAppointmentProposed)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(AppointmentConfirmed appointmentConfirmed)
    {
        Status = AppointmentStatus.Confirmed;
    }


    public void Apply(RescheduleRequested rescheduleRequested)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(AppointmentRescheduled appointmentRescheduled)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(AppointmentCompleted appointmentCompleted)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(AppointmentCancelled appointmentCancelled)
    {
        Status = AppointmentStatus.Cancelled;
    }


    public void Apply(AppointmentNoShowRecorded appointmentNoShowRecorded)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }

}


