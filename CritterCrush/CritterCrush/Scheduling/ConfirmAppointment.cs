namespace CritterCrush.Scheduling;

/// <summary>The counterparty accepted the proposed time</summary>
public record AppointmentConfirmed(Guid OwnerId, Guid ShelterId, DateTimeOffset ConfirmedAt) : IShelterEvent, IOwnerEvent;

public record ConfirmAppointment(Guid AppointmentId);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class ConfirmAppointmentEndpoint
{
    public static ProblemDetails Validate(ConfirmAppointment command, Appointment appointment)
    {
        // One question — is this still Proposed — asked once. The other arms differ only in what
        // they tell the caller; delete any of them and `_` still refuses, which is the test for
        // whether an arm belongs. 404 for a stream that does not exist is Wolverine's own guard on
        // the required Appointment below, so there is nothing to write for it here.
        return appointment.Status switch
        {
            AppointmentStatus.Proposed => WolverineContinue.NoProblems,
            AppointmentStatus.Confirmed => new ProblemDetails { Detail = "This appointment is already confirmed", Status = 400 },
            AppointmentStatus.Cancelled => new ProblemDetails { Detail = "This appointment was cancelled", Status = 400 },
            _ => new ProblemDetails { Detail = "This appointment is already closed", Status = 400 }
        };
    }

    [WolverinePost("/api/scheduling/confirmappointment")]
    [EmptyResponse]
    public static EventsToAppend Post(ConfirmAppointment command, [WriteModel] Appointment appointment) =>
        [new AppointmentConfirmed(appointment.OwnerId, appointment.ShelterId, DateTimeOffset.UtcNow)];
}
