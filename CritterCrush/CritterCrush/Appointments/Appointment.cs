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
        return new Appointment
        {
            Id = homeCheckAppointmentProposed.AppointmentId,
            OwnerId = homeCheckAppointmentProposed.OwnerId,
            ShelterId = homeCheckAppointmentProposed.ShelterId,
            DogId = homeCheckAppointmentProposed.DogId,
            Kind = "HomeCheck",
            Status = "Proposed",
            ScheduledFor = homeCheckAppointmentProposed.ProposedFor
        };
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
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(AppointmentConfirmed appointmentConfirmed)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(RescheduleRequested rescheduleRequested)
    {
        RescheduleRequested = true;
    }


    public void Apply(AppointmentRescheduled appointmentRescheduled)
    {
        ScheduledFor = appointmentRescheduled.ScheduledFor;
        RescheduleRequested = false;
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


