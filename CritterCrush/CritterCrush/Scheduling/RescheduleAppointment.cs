namespace CritterCrush.Scheduling;

/// <summary>The shelter moved the appointment to a new time</summary>
public record AppointmentRescheduled(Guid OwnerId, Guid ShelterId, DateTimeOffset ScheduledFor);

public record RescheduleAppointment(Guid AppointmentId, DateTimeOffset ScheduledFor);

public record RescheduleAppointmentResponse();

/// <summary>
/// Only a requested move is honoured. The board draws Reschedule strictly downstream of Request
/// Reschedule, so a unilateral move is refused here — and the model carries that as a hotspot,
/// because whether a shelter may move an appointment on its own is a real question the board did
/// not settle.
/// </summary>
public static class RescheduleAppointmentEndpoint
{
    public static ProblemDetails Validate(RescheduleAppointment command, [ReadModel] Appointment? appointment)
    {
        if (appointment is null) return Refusals.NoSuchAppointment;
        if (!appointment.RescheduleRequested)
        {
            return new ProblemDetails { Detail = "Nobody asked to move this appointment", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/rescheduleappointment")]
    public static (RescheduleAppointmentResponse, EventsToAppend) Post(RescheduleAppointment command, [WriteModel] Appointment appointment) =>
        (new RescheduleAppointmentResponse(),
            [new AppointmentRescheduled(appointment.OwnerId, appointment.ShelterId, command.ScheduledFor)]);
}
