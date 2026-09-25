namespace CritterCrush.Volunteering;

public record ApproveVolunteer(Guid ApplicantOwnerId);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class ApproveVolunteerEndpoint
{
    public static ProblemDetails Validate(ApproveVolunteer command, VolunteerApplication volunteerApplication)
    {
        // The model's refusing scenarios arrange prior events, so these refusals are about
        // volunteerApplication's state, not the request's shape. It is never null — see the 404 below.
        // TODO guard: return new ProblemDetails { Detail = "This application has not been reviewed", Status = 400 };
        // 404 ("No volunteer application with that id") is Wolverine's own guard on the required VolunteerApplication below:
        // it answers before this method runs, so there is no guard to write here. A null
        // check on volunteerApplication would be unreachable code that looks load-bearing.
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/volunteering/approvevolunteer")]
    [EmptyResponse]
    public static EventsToAppend Post(ApproveVolunteer command, [WriteModel] VolunteerApplication volunteerApplication)
    {
        // The decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Answering with a body instead of 204: drop [EmptyResponse], declare the response
        // record, and return it beside the events as a tuple.
        // Fill this in and delete the throw — the shape is:
        //     return [new VolunteerApproved(/* … */)];
        throw new NotImplementedException("TODO: ApproveVolunteer — decide which events this slice appends");
    }

}


