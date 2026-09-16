namespace CritterCrush.Scheduling;

/// <summary>The counterparty never arrived</summary>
public record AppointmentNoShowRecorded(Guid OwnerId, Guid ShelterId, DateTimeOffset RecordedAt);

public record RecordAppointmentNoShow(Guid AppointmentId);

public record RecordAppointmentNoShowResponse();

/// <summary>
/// A no-show is a kind of completion — somebody turned up to an empty doorstep — so like completion
/// it is reachable only from Confirmed.
/// </summary>
public static class RecordAppointmentNoShowEndpoint
{
    public static ProblemDetails Validate(RecordAppointmentNoShow command, [ReadModel] Appointment? appointment)
    {
        if (appointment is null) return Refusals.NoSuchAppointment;
        if (appointment.IsClosed)
        {
            return new ProblemDetails { Detail = "This appointment is already closed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/recordappointmentnoshow")]
    public static (RecordAppointmentNoShowResponse, EventsToAppend) Post(RecordAppointmentNoShow command, [WriteModel] Appointment appointment) =>
        (new RecordAppointmentNoShowResponse(),
            [new AppointmentNoShowRecorded(appointment.OwnerId, appointment.ShelterId, DateTimeOffset.UtcNow)]);
}
