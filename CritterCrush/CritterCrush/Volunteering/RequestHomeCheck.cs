namespace CritterCrush.Volunteering;

/// <summary>A home check is needed and awaits a volunteer</summary>
public record HomeCheckRequested(Guid ApplicationId, Guid OwnerId, Guid ShelterId);

public record RequestHomeCheck(Guid HomeCheckId, Guid ApplicationId, Guid OwnerId, Guid ShelterId);

public record RequestHomeCheckResponse();

/// <summary>
/// Starts the HomeCheck stream, and the caller supplies the id — which is the easy case: a
/// client-supplied identity makes a creating command specifiable with no derivation at all.
///
/// `applicationId` is carried as data and nothing checks it. The board draws "Application Reviewed"
/// before this step as context, and its own note calls the home check "deliberately advisory, not a
/// hard precondition" — the model records that rather than inventing a rule.
/// </summary>
public static class RequestHomeCheckEndpoint
{
    public static ProblemDetails Validate(RequestHomeCheck command, [ReadModel] HomeCheck? homeCheck)
    {
        // Null is the expected state: this command creates the stream.
        if (homeCheck is not null)
        {
            return new ProblemDetails { Detail = "This home check has already been requested", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/requesthomecheck")]
    public static (RequestHomeCheckResponse, EventsToAppend) Post(RequestHomeCheck command, [WriteModel] HomeCheck? homeCheck) =>
        (new RequestHomeCheckResponse(),
            [new HomeCheckRequested(command.ApplicationId, command.OwnerId, command.ShelterId)]);
}
