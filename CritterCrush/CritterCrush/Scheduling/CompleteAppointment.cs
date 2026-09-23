namespace CritterCrush.Scheduling;

public record CompleteAppointment(Guid AppointmentId);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class CompleteAppointmentEndpoint
{
    /// <summary>A visit that happened was a visit somebody agreed to.</summary>
    public static ProblemDetails Validate(Appointment appointment)
        => appointment.Status == AppointmentStatus.Confirmed
            ? WolverineContinue.NoProblems
            : new ProblemDetails { Detail = "This appointment has not been confirmed", Status = 400 };


    [WolverinePost("/api/scheduling/completeappointment")]
    [EmptyResponse]
    public static AppointmentCompleted Post(CompleteAppointment command, [WriteModel] Appointment appointment)
        => new AppointmentCompleted(appointment.OwnerId, appointment.ShelterId, DateTimeOffset.UtcNow);

}
