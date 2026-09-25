namespace CritterCrush.Scheduling;

public record RequestReschedule(Guid AppointmentId, DateTimeOffset RequestedFor, string Reason);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RequestRescheduleEndpoint
{
    /// <summary>Only an appointment that is still going to happen can be moved.</summary>
    public static ProblemDetails Validate(Appointment appointment)
        => AppointmentStatus.IsClosed(appointment.Status)
            ? new ProblemDetails { Detail = "This appointment is already closed", Status = 400 }
            : WolverineContinue.NoProblems;


    [WolverinePost("/api/scheduling/requestreschedule")]
    [EmptyResponse]
    public static AppointmentRescheduleRequested Post(RequestReschedule command, [WriteModel] Appointment appointment)
        => new AppointmentRescheduleRequested(
            appointment.OwnerId, appointment.ShelterId, command.RequestedFor, command.Reason);

}
