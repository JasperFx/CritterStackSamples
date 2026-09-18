namespace CritterCrush.Scheduling;

public record CompleteAppointment(Guid AppointmentId);

/// <inheritdoc cref="ConfirmAppointmentEndpoint"/>
public static class CompleteAppointmentEndpoint
{
    public static ProblemDetails Validate(Appointment appointment)
    {
        // Completion is reachable only from Confirmed. Stated as the state it REQUIRES, so a state
        // added later refuses by default rather than falling through to success.
        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return new ProblemDetails { Detail = "This appointment has not been confirmed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/completeappointment")]
    [EmptyResponse]
    public static EventsToAppend Post(CompleteAppointment command, [WriteModel] Appointment appointment) =>
        [new AppointmentCompleted(appointment.OwnerId, appointment.ShelterId, DateTimeOffset.UtcNow)];
}
