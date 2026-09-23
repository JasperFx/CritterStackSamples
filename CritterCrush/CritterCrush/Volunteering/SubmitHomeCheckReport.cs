namespace CritterCrush.Volunteering;

public record SubmitHomeCheckReport([property: Identity] Guid HomeCheckId, string Outcome, string Notes);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class SubmitHomeCheckReportEndpoint
{
    public static ProblemDetails Validate(SubmitHomeCheckReport command, HomeCheck homeCheck)
    {
        // The model's refusing scenarios arrange prior events, so these refusals are about
        // homeCheck's state, not the request's shape. It is never null — see the 404 below.
        // TODO guard: return new ProblemDetails { Detail = "Nobody has accepted this home check", Status = 400 };
        // 404 ("No home check with that id") is Wolverine's own guard on the required HomeCheck below:
        // it answers before this method runs, so there is no guard to write here. A null
        // check on homeCheck would be unreachable code that looks load-bearing.
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/volunteering/submithomecheckreport")]
    [EmptyResponse]
    public static EventsToAppend Post(SubmitHomeCheckReport command, [WriteModel] HomeCheck homeCheck)
    {
        // The decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Answering with a body instead of 204: drop [EmptyResponse], declare the response
        // record, and return it beside the events as a tuple.
        // Fill this in and delete the throw — the shape is:
        //     return [new HomeCheckReportSubmitted(/* … */)];
        throw new NotImplementedException("TODO: SubmitHomeCheckReport — decide which events this slice appends");
    }

}


