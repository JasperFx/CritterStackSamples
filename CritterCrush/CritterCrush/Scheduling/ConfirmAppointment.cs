namespace CritterCrush.Scheduling;

public record ConfirmAppointment(Guid AppointmentId);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class ConfirmAppointmentEndpoint
{
    /// <summary>Only a proposal can be confirmed — every other state has its own reason why not.</summary>
    public static ProblemDetails Validate(Appointment appointment)
        => appointment.Status switch
        {
            AppointmentStatus.Proposed => WolverineContinue.NoProblems,
            AppointmentStatus.Cancelled => new ProblemDetails { Detail = "This appointment was cancelled", Status = 400 },
            AppointmentStatus.Confirmed => new ProblemDetails { Detail = "This appointment is already confirmed", Status = 400 },
            _ => new ProblemDetails { Detail = "This appointment is already closed", Status = 400 }
        };


    [WolverinePost("/api/scheduling/confirmappointment")]
    [EmptyResponse]
    public static AppointmentConfirmed Post(ConfirmAppointment command, [WriteModel] Appointment appointment)
        => new AppointmentConfirmed(appointment.OwnerId, appointment.ShelterId, DateTimeOffset.UtcNow);

}
