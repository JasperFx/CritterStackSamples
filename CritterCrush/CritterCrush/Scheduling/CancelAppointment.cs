namespace CritterCrush.Scheduling;

public record CancelAppointment(Guid AppointmentId, string Reason);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class CancelAppointmentEndpoint
{
    /// <summary>An appointment that is still going to happen is the only one worth cancelling.</summary>
    public static ProblemDetails Validate(Appointment appointment)
        => AppointmentStatus.IsClosed(appointment.Status)
            ? new ProblemDetails { Detail = "This appointment is already closed", Status = 400 }
            : WolverineContinue.NoProblems;


    [WolverinePost("/api/scheduling/cancelappointment")]
    [EmptyResponse]
    public static AppointmentCancelled Post(CancelAppointment command, [WriteModel] Appointment appointment)
    {
        // WasConfirmed rides on the event because the counting views have no prior state to consult:
        // only the aggregate knows which bucket this appointment was sitting in.
        return new AppointmentCancelled(
            appointment.OwnerId,
            appointment.ShelterId,
            appointment.Status == AppointmentStatus.Confirmed,
            command.Reason);
    }

}
