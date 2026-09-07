namespace CritterCrush.Appointments;

/// <summary>The appointment will not happen</summary>
public record AppointmentCancelled(Guid AppointmentId, Guid OwnerId, string Reason, DateTimeOffset CancelledAt);

public record CancelAppointmentRequest(Guid AppointmentId, string Reason);

public record CancelAppointmentResponse();

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class CancelAppointmentEndpoint
{
    public static ProblemDetails Validate(CancelAppointmentRequest request)
    {
        // TODO guard: return new ProblemDetails { Detail = "This appointment is already completed", Status = 400 };
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/appointments/cancelappointment")]
    public static (CancelAppointmentResponse, EventsToAppend) Post(CancelAppointmentRequest request, [WriteModel] Appointment? appointment)
    {
        // The decision. Nothing to append is `return (..., []);` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Fill this in and delete the throw — the shape is:
        //     return (new CancelAppointmentResponse(/* … */), [new AppointmentCancelled(/* … */)]);
        throw new NotImplementedException("TODO: CancelAppointment — decide which events this slice appends, and what to answer with");
    }

}


