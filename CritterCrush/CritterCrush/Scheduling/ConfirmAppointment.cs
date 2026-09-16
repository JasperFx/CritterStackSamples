namespace CritterCrush.Scheduling;

/// <summary>The counterparty accepted the proposed time</summary>
public record AppointmentConfirmed(Guid OwnerId, Guid ShelterId, DateTimeOffset ConfirmedAt);

public record ConfirmAppointment(Guid AppointmentId);

public record ConfirmAppointmentResponse();

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class ConfirmAppointmentEndpoint
{
    public static ProblemDetails Validate(ConfirmAppointment command, [ReadModel] Appointment? appointment)
    {
        if (appointment is null) return Refusals.NoSuchAppointment;
        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            return new ProblemDetails { Detail = "This appointment was cancelled", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/confirmappointment")]
    public static (ConfirmAppointmentResponse, EventsToAppend) Post(ConfirmAppointment command, [WriteModel] Appointment appointment) =>
        (new ConfirmAppointmentResponse(),
            [new AppointmentConfirmed(appointment.OwnerId, appointment.ShelterId, DateTimeOffset.UtcNow)]);
}
