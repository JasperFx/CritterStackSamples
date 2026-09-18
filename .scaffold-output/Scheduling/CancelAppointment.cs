namespace CritterCrush.Scheduling;

/// <summary>
/// The appointment will not happen. `wasConfirmed` is carried because the counting views cannot otherwise know which of their buckets this appointment was sitting in, and the aggregate is the only place that knows — a projection has no prior state to consult.
/// 
/// </summary>
public record AppointmentCancelled(Guid OwnerId, Guid ShelterId, bool WasConfirmed, string Reason) : IOwnerEvent, IShelterEvent;

public record CancelAppointment(Guid AppointmentId, string Reason);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class CancelAppointmentEndpoint
{
    public static ProblemDetails Validate(CancelAppointment command, Appointment appointment)
    {
        // The model's refusing scenarios arrange prior events, so these refusals are about
        // appointment's state, not the request's shape. It is never null — see the 404 below.
        // TODO guard: return new ProblemDetails { Detail = "This appointment is already closed", Status = 400 };
        // 404 ("No appointment with that id") is Wolverine's own guard on the required Appointment below:
        // it answers before this method runs, so there is no guard to write here. A null
        // check on appointment would be unreachable code that looks load-bearing.
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/scheduling/cancelappointment")]
    [EmptyResponse]
    public static EventsToAppend Post(CancelAppointment command, [WriteModel] Appointment appointment)
    {
        // The decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Answering with a body instead of 204: drop [EmptyResponse], declare the response
        // record, and return it beside the events as a tuple.
        // Fill this in and delete the throw — the shape is:
        //     return [new AppointmentCancelled(/* … */)];
        throw new NotImplementedException("TODO: CancelAppointment — decide which events this slice appends");
    }

}


