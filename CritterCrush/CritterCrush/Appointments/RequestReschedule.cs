namespace CritterCrush.Appointments;

/// <summary>The owner cannot make the proposed time and suggested another</summary>
public record RescheduleRequested(Guid AppointmentId, Guid OwnerId, string Reason, DateTimeOffset PreferredFor, DateTimeOffset RequestedAt);

public record RequestReschedule(Guid AppointmentId, string Reason, DateTimeOffset PreferredFor);

public record RequestRescheduleResponse(Guid AppointmentId, DateTimeOffset PreferredFor);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RequestRescheduleEndpoint
{
    public static ProblemDetails Validate(RequestReschedule command, [ReadModel] Appointment? appointment)
    {
        // Null means the stream does not exist yet: there is nothing to ask a different time for.
        if (appointment is null)
        {
            return new ProblemDetails { Detail = "No such appointment", Status = 404 };
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return new ProblemDetails { Detail = "A reason for the reschedule is required", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/appointments/requestreschedule")]
    public static (RequestRescheduleResponse, EventsToAppend) Post(RequestReschedule command, [WriteModel] Appointment appointment)
    {
        // The owner is whoever the appointment was proposed to; the request carries only the
        // appointment and the wish. The timestamp rides on the event, never in Apply.
        var requested = new RescheduleRequested(
            appointment.Id,
            appointment.OwnerId,
            command.Reason,
            command.PreferredFor,
            DateTimeOffset.UtcNow);

        return (new RequestRescheduleResponse(appointment.Id, command.PreferredFor), [requested]);
    }
}
