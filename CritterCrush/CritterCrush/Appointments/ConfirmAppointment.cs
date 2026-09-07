namespace CritterCrush.Appointments;

/// <summary>The owner accepted the proposed time</summary>
public record AppointmentConfirmed(Guid AppointmentId, DateTimeOffset ConfirmedAt);

public record ConfirmAppointmentRequest(Guid AppointmentId);

public record ConfirmAppointmentResponse();

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class ConfirmAppointmentEndpoint
{
    public static ProblemDetails Validate(ConfirmAppointmentRequest request)
    {
        // TODO guard: return new ProblemDetails { Detail = "This appointment was cancelled", Status = 400 };
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/appointments/confirmappointment")]
    public static (ConfirmAppointmentResponse, EventsToAppend) Post(ConfirmAppointmentRequest request, [WriteModel] Appointment? appointment)
    {
        // The decision. Nothing to append is `return (..., []);` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Fill this in and delete the throw — the shape is:
        //     return (new ConfirmAppointmentResponse(/* … */), [new AppointmentConfirmed(/* … */)]);
        throw new NotImplementedException("TODO: ConfirmAppointment — decide which events this slice appends, and what to answer with");
    }

}


