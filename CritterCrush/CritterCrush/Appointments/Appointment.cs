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

    public static Appointment Create(HomeCheckAppointmentProposed proposed)
    {
        return new Appointment
        {
            Id = proposed.AppointmentId,
            OwnerId = proposed.OwnerId,
            ShelterId = proposed.ShelterId,
            DogId = proposed.DogId,
            Kind = "HomeCheck",
            Status = "Proposed",
            ScheduledFor = proposed.ProposedFor
        };
    }


    public void Apply(HomeCheckAppointmentProposed proposed)
    {
        Id = proposed.AppointmentId;
        OwnerId = proposed.OwnerId;
        ShelterId = proposed.ShelterId;
        DogId = proposed.DogId;
        Kind = "HomeCheck";
        Status = "Proposed";
        ScheduledFor = proposed.ProposedFor;
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
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(AppointmentRescheduled appointmentRescheduled)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }


    public void Apply(AppointmentCompleted completed)
    {
        Status = "Completed";
    }


    public void Apply(AppointmentCancelled cancelled)
    {
        Status = "Cancelled";
    }


    public void Apply(AppointmentNoShowRecorded appointmentNoShowRecorded)
    {
        // TODO: fold this event into the state. Deterministic only —
        // timestamps belong on the event record, never DateTimeOffset.UtcNow here.
    }

}


