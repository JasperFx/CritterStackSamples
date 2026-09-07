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

    public static Appointment Create(HomeCheckAppointmentProposed proposed)
    {
        var appointment = new Appointment();
        appointment.Apply(homeCheckAppointmentProposed);
        return appointment;
    }


    public void Apply(HomeCheckAppointmentProposed proposed)
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


    public void Apply(FosterHandoverAppointmentProposed proposed)
    {
        Id = proposed.AppointmentId;
        OwnerId = proposed.OwnerId;
        ShelterId = proposed.ShelterId;
        DogId = proposed.DogId;
        Kind = "FosterHandover";
        Status = "Proposed";
        ScheduledFor = proposed.ProposedFor;
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
        Status = AppointmentStatus.Confirmed;
    }


    public void Apply(RescheduleRequested rescheduleRequested)
    {
        // The proposed time stands until the shelter actually moves it; only the flag changes.
        RescheduleRequested = true;
    }


    public void Apply(AppointmentRescheduled appointmentRescheduled)
    {
        ScheduledFor = appointmentRescheduled.ScheduledFor;
        RescheduleRequested = false;
    }


    public void Apply(AppointmentCompleted completed)
    {
        Status = "Completed";
        RescheduleRequested = false;
    }


    public void Apply(AppointmentCancelled cancelled)
    {
        Status = AppointmentStatus.Cancelled;
    }


    public void Apply(AppointmentNoShowRecorded appointmentNoShowRecorded)
    {
        Status = "NoShow";
    }

}


