namespace CritterCrush.Scheduling;

/// <summary>
/// The appointment will not happen. WasConfirmed is carried because the counting views cannot
/// otherwise know which of their buckets this appointment was sitting in, and the aggregate is the
/// only place that knows — a projection has no prior state to consult.
/// </summary>
public record AppointmentCancelled(Guid OwnerId, Guid ShelterId, bool WasConfirmed, string Reason);

public record CancelAppointment(Guid AppointmentId, string Reason);

public record CancelAppointmentResponse();

/// <summary>
/// The one closing event reachable from EITHER state — a proposal nobody answered can be cancelled
/// just as a confirmed visit can. That is why this event, alone among the three, tells the views
/// where it came from.
/// </summary>
public static class CancelAppointmentEndpoint
{
    public static ProblemDetails Validate(CancelAppointment command, [ReadModel] Appointment? appointment)
    {
        if (appointment is null) return Refusals.NoSuchAppointment;
        if (appointment.IsClosed)
        {
            return new ProblemDetails { Detail = "This appointment is already closed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/scheduling/cancelappointment")]
    public static (CancelAppointmentResponse, EventsToAppend) Post(CancelAppointment command, [WriteModel] Appointment appointment) =>
        (new CancelAppointmentResponse(),
            [
                new AppointmentCancelled(
                    appointment.OwnerId,
                    appointment.ShelterId,
                    appointment.Status == AppointmentStatus.Confirmed,
                    command.Reason)
            ]);
}
