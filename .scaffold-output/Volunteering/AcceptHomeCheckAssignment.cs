namespace CritterCrush.Volunteering;

/// <summary>A volunteer took the assignment and proposed a time to visit</summary>
public record HomeCheckAssignmentAccepted(Guid AssignmentId, Guid OwnerId, Guid ShelterId, Guid VolunteerOwnerId, DateTimeOffset ProposedFor);

public record AcceptHomeCheckAssignment(Guid HomeCheckId, Guid VolunteerOwnerId, DateTimeOffset ProposedFor);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class AcceptHomeCheckAssignmentEndpoint
{
    public static ProblemDetails Validate(AcceptHomeCheckAssignment command, HomeCheck homeCheck)
    {
        // The model's refusing scenarios arrange prior events, so these refusals are about
        // homeCheck's state, not the request's shape. It is never null — see the 404 below.
        // TODO guard: return new ProblemDetails { Detail = "This home check is already assigned", Status = 400 };
        // 404 ("No home check with that id") is Wolverine's own guard on the required HomeCheck below:
        // it answers before this method runs, so there is no guard to write here. A null
        // check on homeCheck would be unreachable code that looks load-bearing.
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/volunteering/accepthomecheckassignment")]
    [EmptyResponse]
    public static EventsToAppend Post(AcceptHomeCheckAssignment command, [WriteModel] HomeCheck homeCheck)
    {
        // HOTSPOT (from the model): The board arranges BOTH "Home Check Requested" and "Volunteer Approved" before this command — two aggregates in one scenario. This model cannot say that: a curated `given:` entry can name a `stream:` (bobcat#311) but not an AGGREGATE, so every arranged event lands on this slice's own HomeCheck stream. Enforcing "only an approved volunteer may accept" would therefore be a guard no scenario could arrange, so it is deliberately NOT implemented — an unspecifiable guard is worse than a recorded gap. Filed as bobcat#320.

        // The decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Answering with a body instead of 204: drop [EmptyResponse], declare the response
        // record, and return it beside the events as a tuple.
        // Fill this in and delete the throw — the shape is:
        //     return [new HomeCheckAssignmentAccepted(/* … */)];
        throw new NotImplementedException("TODO: AcceptHomeCheckAssignment — decide which events this slice appends");
    }

}


