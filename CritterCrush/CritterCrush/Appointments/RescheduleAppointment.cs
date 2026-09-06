namespace CritterCrush.Appointments;

public record AppointmentRescheduled();

public record RescheduleAppointmentRequest(Guid AppointmentId, DateTimeOffset ScheduledFor);

public record RescheduleAppointmentResponse();

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RescheduleAppointmentEndpoint
{
    public static ProblemDetails Validate(RescheduleAppointmentRequest request)
    {
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/appointments/rescheduleappointment")]
    public static (RescheduleAppointmentResponse, EventsToAppend) Post(RescheduleAppointmentRequest request, [WriteModel] Appointment? appointment)
    {
        // TODO: the decision. Nothing to append is `return (..., []);` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        return (new RescheduleAppointmentResponse(/* TODO */), [new AppointmentRescheduled(/* TODO */)]);
    }

}


