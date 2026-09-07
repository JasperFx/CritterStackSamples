namespace CritterCrush.Appointments;

/// <summary>The shelter moved the appointment to a new time</summary>
public record AppointmentRescheduled(Guid AppointmentId, Guid OwnerId, DateTimeOffset ScheduledFor, DateTimeOffset RescheduledAt);

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
        // The decision. Nothing to append is `return (..., []);` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Fill this in and delete the throw — the shape is:
        //     return (new RescheduleAppointmentResponse(/* … */), [new AppointmentRescheduled(/* … */)]);
        throw new NotImplementedException("TODO: RescheduleAppointment — decide which events this slice appends, and what to answer with");
    }

}


