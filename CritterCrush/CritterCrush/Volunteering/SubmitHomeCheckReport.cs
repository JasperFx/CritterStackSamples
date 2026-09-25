namespace CritterCrush.Volunteering;

public record SubmitHomeCheckReport([property: Identity] Guid HomeCheckId, string Outcome, string Notes);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class SubmitHomeCheckReportEndpoint
{
    /// <summary>A report comes from the volunteer who took the visit.</summary>
    public static ProblemDetails Validate(HomeCheck homeCheck)
        => homeCheck.Status == HomeCheckStatus.Accepted
            ? WolverineContinue.NoProblems
            : new ProblemDetails { Detail = "Nobody has accepted this home check", Status = 400 };


    [WolverinePost("/api/volunteering/submithomecheckreport")]
    [EmptyResponse]
    public static HomeCheckReportSubmitted Post(SubmitHomeCheckReport command, [WriteModel] HomeCheck homeCheck)
        => new HomeCheckReportSubmitted(
            homeCheck.ApplicationId, homeCheck.OwnerId, homeCheck.ShelterId, command.Outcome, command.Notes);

}
