namespace CritterCrush.Appointments;

/// <summary>The appointment will not happen</summary>
public record AppointmentCancelled(Guid AppointmentId, Guid OwnerId, string Reason, DateTimeOffset CancelledAt);

public record CancelAppointmentRequest(Guid AppointmentId, string Reason);

public record CancelAppointmentResponse(Guid AppointmentId, string Status);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class CancelAppointmentEndpoint
{
    public static ProblemDetails Validate(CancelAppointmentRequest request, [ReadModel] Appointment? appointment)
    {
        // The refusals are about the appointment's state, not the request's shape.
        // Null means the stream does not exist yet.
        if (appointment is null)
        {
            return new ProblemDetails { Detail = "This appointment does not exist", Status = 404 };
        }

        if (appointment.Status == "Completed")
        {
            return new ProblemDetails { Detail = "This appointment is already completed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/appointments/cancelappointment")]
    public static (CancelAppointmentResponse, EventsToAppend) Post(CancelAppointmentRequest request, [WriteModel] Appointment appointment)
    {
        // Cancelling twice is a no-op rather than a refusal: the outcome the caller asked for
        // already holds, so answer honestly and append nothing.
        if (appointment.Status == "Cancelled")
        {
            return (new CancelAppointmentResponse(appointment.Id, appointment.Status), []);
        }

        var cancelled = new AppointmentCancelled(
            appointment.Id,
            appointment.OwnerId,
            request.Reason,
            DateTimeOffset.UtcNow);

        return (new CancelAppointmentResponse(appointment.Id, "Cancelled"), [cancelled]);
    }
}
