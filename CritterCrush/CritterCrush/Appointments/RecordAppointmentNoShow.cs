namespace CritterCrush.Appointments;

public record AppointmentNoShowRecorded();

public record RecordAppointmentNoShowRequest(Guid AppointmentId);

public record RecordAppointmentNoShowResponse();

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RecordAppointmentNoShowEndpoint
{
    public static ProblemDetails Validate(RecordAppointmentNoShowRequest request)
    {
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/appointments/recordappointmentnoshow")]
    public static (RecordAppointmentNoShowResponse, EventsToAppend) Post(RecordAppointmentNoShowRequest request, [WriteModel] Appointment? appointment)
    {
        // TODO: the decision. Nothing to append is `return (..., []);` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        return (new RecordAppointmentNoShowResponse(/* TODO */), [new AppointmentNoShowRecorded(/* TODO */)]);
    }

}


