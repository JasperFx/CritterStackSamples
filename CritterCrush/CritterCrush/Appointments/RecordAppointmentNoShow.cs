namespace CritterCrush.Appointments;

/// <summary>Nobody was there when the shelter turned up</summary>
public record AppointmentNoShowRecorded(Guid AppointmentId, Guid OwnerId, DateTimeOffset RecordedAt);

public record RecordAppointmentNoShowRequest(Guid AppointmentId);

public record RecordAppointmentNoShowResponse(Guid AppointmentId, Guid OwnerId, DateTimeOffset RecordedAt);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RecordAppointmentNoShowEndpoint
{
    public static ProblemDetails Validate(RecordAppointmentNoShowRequest request, [ReadModel] Appointment? appointment)
    {
        // Null means the stream does not exist yet: there is no appointment to have missed.
        if (appointment is null)
        {
            return new ProblemDetails { Detail = "This appointment does not exist", Status = 404 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/appointments/recordappointmentnoshow")]
    public static (RecordAppointmentNoShowResponse, EventsToAppend) Post(RecordAppointmentNoShowRequest request, [WriteModel] Appointment appointment)
    {
        // The timestamp lives on the event record so a rebuild folds the same value the
        // original run did.
        var recorded = new AppointmentNoShowRecorded(appointment.Id, appointment.OwnerId, DateTimeOffset.UtcNow);

        return (new RecordAppointmentNoShowResponse(recorded.AppointmentId, recorded.OwnerId, recorded.RecordedAt), [recorded]);
    }
}
