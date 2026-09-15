namespace CritterCrush.Appointments;

/// <summary>The owner accepted the proposed time</summary>
public record AppointmentConfirmed(Guid AppointmentId, Guid OwnerId, DateTimeOffset ConfirmedAt);

public record ConfirmAppointment(Guid AppointmentId);

public record ConfirmAppointmentResponse(Guid AppointmentId, string Status);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class ConfirmAppointmentEndpoint
{
    public static ProblemDetails Validate(ConfirmAppointment command, [ReadModel] Appointment? appointment)
    {
        // The model's refusing scenarios arrange prior events, so these refusals are about
        // appointment's state, not the request's shape. Null means the stream does not exist yet.
        if (appointment is null)
        {
            return new ProblemDetails { Detail = "No such appointment", Status = 404 };
        }

        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            return new ProblemDetails { Detail = "This appointment was cancelled", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/appointments/confirmappointment")]
    public static (ConfirmAppointmentResponse, EventsToAppend) Post(ConfirmAppointment command, [WriteModel] Appointment appointment)
    {
        // Validate already refused a missing or cancelled appointment, so the decision here is
        // only to record the owner's acceptance. The timestamp lives on the event, not in Apply.
        var confirmed = new AppointmentConfirmed(appointment.Id, appointment.OwnerId, DateTimeOffset.UtcNow);

        return (new ConfirmAppointmentResponse(appointment.Id, AppointmentStatus.Confirmed), [confirmed]);
    }
}
