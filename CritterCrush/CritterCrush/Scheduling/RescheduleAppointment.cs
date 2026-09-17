namespace CritterCrush.Scheduling;

/// <summary>The shelter moved it</summary>
public record AppointmentRescheduled(Guid OwnerId, Guid ShelterId, DateTimeOffset ScheduledFor);

public record RescheduleAppointment(Guid AppointmentId, DateTimeOffset ScheduledFor);

/// <inheritdoc cref="ConfirmAppointmentEndpoint"/>
public static class RescheduleAppointmentEndpoint
{
    public static ProblemDetails Validate(RescheduleAppointment command, Appointment appointment)
    {
        // TWO conditions, and both are requirements rather than exclusions. Checking only
        // RescheduleRequested would be the trap: that flag survives a cancellation, so asking to
        // move an appointment and then cancelling it would leave it movable.
        if (appointment.IsClosed)
        {
            return new ProblemDetails { Detail = "This appointment is already closed", Status = 400 };
        }

        if (!appointment.RescheduleRequested)
        {
            return new ProblemDetails { Detail = "Nobody asked to move this appointment", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/rescheduleappointment")]
    [EmptyResponse]
    public static EventsToAppend Post(RescheduleAppointment command, [WriteModel] Appointment appointment) =>
        [new AppointmentRescheduled(appointment.OwnerId, appointment.ShelterId, command.ScheduledFor)];
}
