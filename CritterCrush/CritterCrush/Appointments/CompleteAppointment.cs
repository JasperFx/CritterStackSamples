namespace CritterCrush.Appointments;

public record AppointmentCompleted();

public record CompleteAppointmentRequest(Guid AppointmentId, string Notes);

public record CompleteAppointmentResponse();

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class CompleteAppointmentEndpoint
{
    public static ProblemDetails Validate(CompleteAppointmentRequest request)
    {
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/appointments/completeappointment")]
    public static (CompleteAppointmentResponse, EventsToAppend) Post(CompleteAppointmentRequest request, [WriteModel] Appointment? appointment)
    {
        // The decision. Nothing to append is `return (..., []);` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Fill this in and delete the throw — the shape is:
        //     return (new CompleteAppointmentResponse(/* … */), [new AppointmentCompleted(/* … */)]);
        throw new NotImplementedException("TODO: CompleteAppointment — decide which events this slice appends, and what to answer with");
    }

}


