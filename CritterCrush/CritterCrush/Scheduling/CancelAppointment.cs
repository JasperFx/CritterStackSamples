namespace CritterCrush.Scheduling;

/// <summary>Called off before it happened</summary>
public record AppointmentCancelled(Guid OwnerId, Guid ShelterId, bool WasConfirmed, string Reason) : IShelterEvent, IOwnerEvent;

public record CancelAppointment(Guid AppointmentId, string Reason);

/// <inheritdoc cref="ConfirmAppointmentEndpoint"/>
public static class CancelAppointmentEndpoint
{
    public static ProblemDetails Validate(CancelAppointment command, Appointment appointment)
    {
        // Cancellation is the one command reachable from EITHER open state, so the rule it requires
        // is "still open" rather than a single status — and `IsClosed` is that rule stated on the
        // aggregate, not a list of states to exclude.
        if (appointment.IsClosed)
        {
            return new ProblemDetails { Detail = "This appointment is already closed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/cancelappointment")]
    [EmptyResponse]
    public static EventsToAppend Post(CancelAppointment command, [WriteModel] Appointment appointment) =>
    [
        new AppointmentCancelled(appointment.OwnerId, appointment.ShelterId,
            // The queue read model has to know which bucket to take this appointment out of, and
            // after the fold it can no longer tell. So the event carries it.
            WasConfirmed: appointment.Status == AppointmentStatus.Confirmed,
            command.Reason)
    ];
}
