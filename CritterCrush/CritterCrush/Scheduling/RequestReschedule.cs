namespace CritterCrush.Scheduling;

/// <summary>The counterparty asked for a different time</summary>
public record AppointmentRescheduleRequested(Guid OwnerId, Guid ShelterId, DateTimeOffset RequestedFor, string Reason);

public record RequestReschedule(Guid AppointmentId, DateTimeOffset RequestedFor, string Reason);

public record RequestRescheduleResponse();

/// <summary>
/// The counterparty asks; the shelter still owns the move itself, which is the RescheduleAppointment
/// slice. Asking is deliberately not moving.
/// </summary>
public static class RequestRescheduleEndpoint
{
    public static ProblemDetails Validate(RequestReschedule command, [ReadModel] Appointment? appointment)
    {
        if (appointment is null) return Refusals.NoSuchAppointment;
        if (appointment.IsClosed)
        {
            return new ProblemDetails { Detail = "This appointment is already closed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/requestreschedule")]
    public static (RequestRescheduleResponse, EventsToAppend) Post(RequestReschedule command, [WriteModel] Appointment appointment) =>
        (new RequestRescheduleResponse(),
            [new AppointmentRescheduleRequested(appointment.OwnerId, appointment.ShelterId, command.RequestedFor, command.Reason)]);
}
