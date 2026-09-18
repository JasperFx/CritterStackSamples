namespace CritterCrush.Volunteering;

public record AcceptHomeCheckAssignment([property: Identity] Guid HomeCheckId, Guid VolunteerOwnerId, DateTimeOffset ProposedFor);

/// <inheritdoc cref="CritterCrush.Scheduling.ConfirmAppointmentEndpoint"/>
public static class AcceptHomeCheckAssignmentEndpoint
{
    public static ProblemDetails Validate(HomeCheck homeCheck)
    {
        if (homeCheck.Status != HomeCheckStatus.Requested)
        {
            return new ProblemDetails { Detail = "This home check is already assigned", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/accepthomecheckassignment")]
    [EmptyResponse]
    public static HomeCheckAssignmentAccepted Post(AcceptHomeCheckAssignment command, [WriteModel] HomeCheck homeCheck) =>
    new HomeCheckAssignmentAccepted(
            Guid.NewGuid(),
            homeCheck.OwnerId,
            homeCheck.ShelterId,
            command.VolunteerOwnerId,
            command.ProposedFor);
}
