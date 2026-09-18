namespace CritterCrush.Scheduling;

public record RecordAppointmentNoShow(Guid AppointmentId);

/// <summary>
/// A no-show is a kind of completion — somebody turned up to an empty doorstep — so like completion
/// it is reachable only from Confirmed.
/// </summary>
public static class RecordAppointmentNoShowEndpoint
{
    public static ProblemDetails Validate(Appointment appointment)
    {
        // The closed case first, only because it earns a better sentence than the general refusal.
        // It decides nothing: delete it and the required-state check below still refuses.
        if (appointment.IsClosed)
        {
            return new ProblemDetails { Detail = "This appointment is already closed", Status = 400 };
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return new ProblemDetails { Detail = "This appointment has not been confirmed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/recordappointmentnoshow")]
    [EmptyResponse]
    public static AppointmentNoShowRecorded Post(RecordAppointmentNoShow command, [WriteModel] Appointment appointment) =>
        new AppointmentNoShowRecorded(appointment.OwnerId, appointment.ShelterId, DateTimeOffset.UtcNow);
}
