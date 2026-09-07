namespace CritterCrush.Appointments;

/// <summary>The owner cannot make the proposed time and suggested another</summary>
public record RescheduleRequested(Guid AppointmentId, Guid OwnerId, string Reason, DateTimeOffset PreferredFor, DateTimeOffset RequestedAt);

public record RequestRescheduleRequest(Guid AppointmentId, string Reason, DateTimeOffset PreferredFor);

public record RequestRescheduleResponse();

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RequestRescheduleEndpoint
{
    public static ProblemDetails Validate(RequestRescheduleRequest request)
    {
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/appointments/requestreschedule")]
    public static (RequestRescheduleResponse, EventsToAppend) Post(RequestRescheduleRequest request, [WriteModel] Appointment? appointment)
    {
        // The decision. Nothing to append is `return (..., []);` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Fill this in and delete the throw — the shape is:
        //     return (new RequestRescheduleResponse(/* … */), [new RescheduleRequested(/* … */)]);
        throw new NotImplementedException("TODO: RequestReschedule — decide which events this slice appends, and what to answer with");
    }

}


