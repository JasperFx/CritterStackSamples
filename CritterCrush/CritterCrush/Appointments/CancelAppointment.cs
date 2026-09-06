namespace CritterCrush.Appointments;

public record AppointmentCancelled();

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
        // TODO: the decision. Nothing to append is `return (..., []);` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        return (new CancelAppointmentResponse(/* TODO */), [new AppointmentCancelled(/* TODO */)]);
    }

}


