namespace CritterCrush.Scheduling;

/// <summary>The visit happened</summary>
public record AppointmentCompleted(Guid OwnerId, Guid ShelterId, DateTimeOffset CompletedAt);

public record CompleteAppointment(Guid AppointmentId);

public record CompleteAppointmentResponse();

/// <summary>
/// Completion is reachable only from Confirmed, which is what lets AppointmentCompleted carry no
/// prior-state flag: the counting views know it came out of their Confirmed bucket.
/// </summary>
public static class CompleteAppointmentEndpoint
{
    public static ProblemDetails Validate(CompleteAppointment command, [ReadModel] Appointment? appointment)
    {
        if (appointment is null) return Refusals.NoSuchAppointment;
        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return new ProblemDetails { Detail = "This appointment has not been confirmed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/completeappointment")]
    public static (CompleteAppointmentResponse, EventsToAppend) Post(CompleteAppointment command, [WriteModel] Appointment appointment) =>
        (new CompleteAppointmentResponse(),
            [new AppointmentCompleted(appointment.OwnerId, appointment.ShelterId, DateTimeOffset.UtcNow)]);
}
