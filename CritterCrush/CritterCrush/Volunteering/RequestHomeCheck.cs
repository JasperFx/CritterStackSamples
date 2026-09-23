namespace CritterCrush.Volunteering;

public record RequestHomeCheck([property: Identity] Guid HomeCheckId, Guid ApplicationId, Guid OwnerId, Guid ShelterId);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RequestHomeCheckEndpoint
{
    /// <summary>The caller names the home check's id, so asking twice is asking for a stream that exists.</summary>
    public static ProblemDetails Validate(HomeCheck? homeCheck)
        => homeCheck is null
            ? WolverineContinue.NoProblems
            : new ProblemDetails { Detail = "This home check has already been requested", Status = 400 };


    [WolverinePost("/api/volunteering/requesthomecheck")]
    [EmptyResponse]
    public static HomeCheckRequested Post(RequestHomeCheck command, [WriteModel] HomeCheck? homeCheck)
        => new HomeCheckRequested(command.ApplicationId, command.OwnerId, command.ShelterId);

}
