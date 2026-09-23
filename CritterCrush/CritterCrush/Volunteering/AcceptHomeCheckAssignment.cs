namespace CritterCrush.Volunteering;

public record AcceptHomeCheckAssignment([property: Identity] Guid HomeCheckId, Guid VolunteerOwnerId, DateTimeOffset ProposedFor);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class AcceptHomeCheckAssignmentEndpoint
{
    /// <summary>Only an unclaimed home check is there to be taken.</summary>
    public static ProblemDetails Validate(HomeCheck homeCheck)
        => homeCheck.Status == HomeCheckStatus.Requested
            ? WolverineContinue.NoProblems
            : new ProblemDetails { Detail = "This home check is already assigned", Status = 400 };


    [WolverinePost("/api/volunteering/accepthomecheckassignment")]
    [EmptyResponse]
    public static HomeCheckAssignmentAccepted Post(AcceptHomeCheckAssignment command, [WriteModel] HomeCheck homeCheck)
    {
        // The assignment gets its OWN identity, minted here, and that id becomes the Appointment's
        // stream downstream. It cannot be command.HomeCheckId: Marten's stream id space is GLOBAL
        // across aggregate types, so the Appointment would be claiming an id the HomeCheck already
        // owns — `Stream #… already exists in the database`, which is what happens if you try it.
        // Minting it HERE rather than in the automation is what keeps the appointment's stream
        // predictable: an id the automation invents is an id no scenario can assert on.
        return new HomeCheckAssignmentAccepted(
            Guid.NewGuid(),
            homeCheck.OwnerId,
            homeCheck.ShelterId,
            command.VolunteerOwnerId,
            command.ProposedFor);
    }

}
