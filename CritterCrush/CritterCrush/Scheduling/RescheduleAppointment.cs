namespace CritterCrush.Scheduling;

public record RescheduleAppointment(Guid AppointmentId, DateTimeOffset ScheduledFor);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RescheduleAppointmentEndpoint
{
    /// <summary>
    /// The board draws Reschedule only downstream of Request Reschedule, so a move nobody asked for
    /// is refused. Whether the shelter may move an appointment unilaterally — a vet running late, a
    /// volunteer calling in sick — is the model's open hotspot, not this code's to settle.
    /// </summary>
    public static ProblemDetails Validate(Appointment appointment)
    {
        if (AppointmentStatus.IsClosed(appointment.Status))
            return new ProblemDetails { Detail = "This appointment is already closed", Status = 400 };

        return appointment.RescheduleRequested
            ? WolverineContinue.NoProblems
            : new ProblemDetails { Detail = "Nobody asked to move this appointment", Status = 400 };
    }


    [WolverinePost("/api/scheduling/rescheduleappointment")]
    [EmptyResponse]
    public static AppointmentRescheduled Post(RescheduleAppointment command, [WriteModel] Appointment appointment)
        => new AppointmentRescheduled(appointment.OwnerId, appointment.ShelterId, command.ScheduledFor);

}
