namespace CritterCrush.Volunteering;

/// <summary>
/// A volunteer took the assignment and proposed a time to visit. This is the event
/// BookingAppointments waits for — the cross-chapter link, and the reason
/// ProposeHomeCheckAppointment declares no inbound external system.
///
/// AssignmentId is the ASSIGNMENT's own identity, minted here, and deliberately NOT this home
/// check's stream id: BookingAppointments uses it as the Appointment's stream id, and Marten's
/// stream id space is global across aggregate types, so reusing this stream's id would collide.
/// Once written it never changes, so redelivery still lands on the same appointment.
/// </summary>
public record HomeCheckAssignmentAccepted(
    Guid AssignmentId,
    Guid OwnerId,
    Guid ShelterId,
    Guid VolunteerOwnerId,
    DateTimeOffset ProposedFor);

public record AcceptHomeCheckAssignment(Guid HomeCheckId, Guid VolunteerOwnerId, DateTimeOffset ProposedFor);

public record AcceptHomeCheckAssignmentResponse();

public static class AcceptHomeCheckAssignmentEndpoint
{
    public static ProblemDetails Validate(AcceptHomeCheckAssignment command, [ReadModel] HomeCheck? homeCheck)
    {
        if (homeCheck is null) return VolunteeringRefusals.NoSuchHomeCheck;
        if (homeCheck.Status != HomeCheckStatus.Requested)
        {
            return new ProblemDetails { Detail = "This home check is already assigned", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/accepthomecheckassignment")]
    public static (AcceptHomeCheckAssignmentResponse, EventsToAppend) Post(AcceptHomeCheckAssignment command, [WriteModel] HomeCheck homeCheck) =>
        (new AcceptHomeCheckAssignmentResponse(),
            [
                new HomeCheckAssignmentAccepted(
                    Guid.NewGuid(),
                    homeCheck.OwnerId,
                    homeCheck.ShelterId,
                    command.VolunteerOwnerId,
                    command.ProposedFor)
            ]);
}
