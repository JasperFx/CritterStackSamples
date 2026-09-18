namespace CritterCrush.Volunteering;

/// <summary>A home check is needed and awaits a volunteer</summary>
public record HomeCheckRequested(Guid ApplicationId, Guid OwnerId, Guid ShelterId);

public record RequestHomeCheck(Guid HomeCheckId, Guid ApplicationId, Guid OwnerId, Guid ShelterId);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RequestHomeCheckEndpoint
{
    public static ProblemDetails Validate(RequestHomeCheck command, HomeCheck? homeCheck)
    {
        // The model's refusing scenarios arrange prior events, so these refusals are about
        // homeCheck's state, not the request's shape. Null means the stream does not exist yet.
        // TODO guard: return new ProblemDetails { Detail = "This home check has already been requested", Status = 400 };
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/volunteering/requesthomecheck")]
    [EmptyResponse]
    public static EventsToAppend Post(RequestHomeCheck command, [WriteModel] HomeCheck? homeCheck)
    {
        // HOTSPOT (from the model): The board arranges "Application Reviewed" — an event ShelterReviewsApplication owns — before this command, as context rather than as a rule: its own comment calls the home check "deliberately advisory, not a hard precondition on Approve/Reject". So `applicationId` is carried as data and nothing here enforces that the adoption application was reviewed.

        // The decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Answering with a body instead of 204: drop [EmptyResponse], declare the response
        // record, and return it beside the events as a tuple.
        // Fill this in and delete the throw — the shape is:
        //     return [new HomeCheckRequested(/* … */)];
        throw new NotImplementedException("TODO: RequestHomeCheck — decide which events this slice appends");
    }

}


