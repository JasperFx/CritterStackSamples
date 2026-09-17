namespace CritterCrush.Scheduling;

/// <summary>The counterparty asked for a different time</summary>
public record AppointmentRescheduleRequested(Guid OwnerId, Guid ShelterId, DateTimeOffset RequestedFor, string Reason);

public record RequestReschedule(Guid AppointmentId, DateTimeOffset RequestedFor, string Reason);

/// <inheritdoc cref="ConfirmAppointmentEndpoint"/>
public static class RequestRescheduleEndpoint
{
    public static ProblemDetails Validate(RequestReschedule command, Appointment appointment)
    {
        // Asking to move is reachable from either open state — proposed or confirmed — so the rule
        // is "still open", the same one cancellation requires.
        if (appointment.IsClosed)
        {
            return new ProblemDetails { Detail = "This appointment is already closed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/requestreschedule")]
    [EmptyResponse]
    public static EventsToAppend Post(RequestReschedule command, [WriteModel] Appointment appointment) =>
        [new AppointmentRescheduleRequested(appointment.OwnerId, appointment.ShelterId, command.RequestedFor, command.Reason)];
}
