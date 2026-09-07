namespace CritterCrush.Appointments;

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
        // TODO: fold the creating event into the initial state
        return new Appointment();
    }


    public void Apply(HomeCheckAppointmentProposed homeCheckAppointmentProposed)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(FosterHandoverAppointmentProposed fosterHandoverAppointmentProposed)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(SurrenderIntakeAppointmentProposed surrenderIntakeAppointmentProposed)
    {
        Id = surrenderIntakeAppointmentProposed.AppointmentId;
        OwnerId = surrenderIntakeAppointmentProposed.OwnerId;
        ShelterId = surrenderIntakeAppointmentProposed.ShelterId;
        DogId = surrenderIntakeAppointmentProposed.DogId;
        Kind = "SurrenderIntake";
        Status = "Proposed";
        ScheduledFor = surrenderIntakeAppointmentProposed.ProposedFor;
    }


    public void Apply(AppointmentConfirmed appointmentConfirmed)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
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
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(AppointmentNoShowRecorded appointmentNoShowRecorded)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }

}


