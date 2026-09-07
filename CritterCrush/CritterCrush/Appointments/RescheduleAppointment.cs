namespace CritterCrush.Appointments;

/// <summary>The shelter moved the appointment to a new time</summary>
public record AppointmentRescheduled(Guid AppointmentId, Guid OwnerId, DateTimeOffset ScheduledFor, DateTimeOffset RescheduledAt);

public record RescheduleAppointmentRequest(Guid AppointmentId, DateTimeOffset ScheduledFor);

public record RescheduleAppointmentResponse(Guid AppointmentId, DateTimeOffset ScheduledFor);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RescheduleAppointmentEndpoint
{
    public static ProblemDetails Validate(RescheduleAppointmentRequest request, [ReadModel] Appointment? appointment)
    {
        // Null means the stream does not exist yet: there is nothing to move.
        if (appointment is null)
        {
            return new ProblemDetails { Detail = "This appointment does not exist", Status = 404 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/appointments/rescheduleappointment")]
    public static (RescheduleAppointmentResponse, EventsToAppend) Post(
        RescheduleAppointmentRequest request,
        [WriteModel] Appointment appointment)
    {
        // The timestamp is taken here, in the decision, and lives on the event — so a rebuild
        // folds the same value the live run did.
        var rescheduled = new AppointmentRescheduled(
            appointment.Id,
            appointment.OwnerId,
            request.ScheduledFor,
            DateTimeOffset.UtcNow);

        return (new RescheduleAppointmentResponse(appointment.Id, request.ScheduledFor), [rescheduled]);
    }
}
