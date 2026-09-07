namespace CritterCrush.Appointments;

/// <summary>The visit happened and the shelter wrote it up</summary>
public record AppointmentCompleted(Guid AppointmentId, Guid OwnerId, string Notes, DateTimeOffset CompletedAt);

public record CompleteAppointmentRequest(Guid AppointmentId, string Notes);

public record CompleteAppointmentResponse(Guid AppointmentId);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class CompleteAppointmentEndpoint
{
    public static ProblemDetails Validate(CompleteAppointmentRequest request, [ReadModel] Appointment? appointment)
    {
        // Null means the stream does not exist yet: there is nothing to complete.
        if (appointment is null)
        {
            return new ProblemDetails { Detail = "No such appointment", Status = 400 };
        }

        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            return new ProblemDetails { Detail = "Completion notes are required", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/appointments/completeappointment")]
    public static (CompleteAppointmentResponse, EventsToAppend) Post(CompleteAppointmentRequest request, [WriteModel] Appointment appointment)
    {
        // The timestamp is decided here, on the event, so the aggregate's fold stays deterministic.
        var completed = new AppointmentCompleted(request.AppointmentId, appointment.OwnerId, request.Notes, DateTimeOffset.UtcNow);
        return (new CompleteAppointmentResponse(request.AppointmentId), [completed]);
    }
}
