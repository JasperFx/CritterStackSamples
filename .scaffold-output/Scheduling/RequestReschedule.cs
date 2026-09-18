namespace CritterCrush.Scheduling;

/// <summary>The counterparty asked for a different time</summary>
public record AppointmentRescheduleRequested(Guid OwnerId, Guid ShelterId, DateTimeOffset RequestedFor, string Reason);

public record RequestReschedule(Guid AppointmentId, DateTimeOffset RequestedFor, string Reason);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RequestRescheduleEndpoint
{
    public static ProblemDetails Validate(RequestReschedule command, Appointment appointment)
    {
        // The model's refusing scenarios arrange prior events, so these refusals are about
        // appointment's state, not the request's shape. It is never null — see the 404 below.
        // TODO guard: return new ProblemDetails { Detail = "This appointment is already closed", Status = 400 };
        // 404 ("No appointment with that id") is Wolverine's own guard on the required Appointment below:
        // it answers before this method runs, so there is no guard to write here. A null
        // check on appointment would be unreachable code that looks load-bearing.
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/scheduling/requestreschedule")]
    [EmptyResponse]
    public static EventsToAppend Post(RequestReschedule command, [WriteModel] Appointment appointment)
    {
        // The decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Answering with a body instead of 204: drop [EmptyResponse], declare the response
        // record, and return it beside the events as a tuple.
        // Fill this in and delete the throw — the shape is:
        //     return [new AppointmentRescheduleRequested(/* … */)];
        throw new NotImplementedException("TODO: RequestReschedule — decide which events this slice appends");
    }

}


