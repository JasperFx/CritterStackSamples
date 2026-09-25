namespace CritterCrush.Scheduling;

public record RecordAppointmentNoShow(Guid AppointmentId);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RecordAppointmentNoShowEndpoint
{
    /// <summary>
    /// Only a confirmed appointment can be a no-show: somebody has to have agreed to be there.
    /// Stated as the state required rather than the states excluded — the closed check and the
    /// unconfirmed check are one question, and asking it as two `if`s is how a state that is both
    /// gets told the wrong reason.
    /// </summary>
    public static ProblemDetails Validate(Appointment appointment)
        => appointment.Status switch
        {
            AppointmentStatus.Confirmed => WolverineContinue.NoProblems,
            AppointmentStatus.Proposed => new ProblemDetails { Detail = "This appointment has not been confirmed", Status = 400 },
            _ => new ProblemDetails { Detail = "This appointment is already closed", Status = 400 }
        };


    [WolverinePost("/api/scheduling/recordappointmentnoshow")]
    [EmptyResponse]
    public static AppointmentNoShowRecorded Post(RecordAppointmentNoShow command, [WriteModel] Appointment appointment)
        => new AppointmentNoShowRecorded(appointment.OwnerId, appointment.ShelterId, DateTimeOffset.UtcNow);

}
