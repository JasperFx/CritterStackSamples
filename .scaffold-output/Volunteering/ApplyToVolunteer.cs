namespace CritterCrush.Volunteering;

/// <summary>Somebody asked to volunteer</summary>
public record VolunteerApplicationSubmitted(Guid ApplicantOwnerId, string AreasOfInterest);

public record ApplyToVolunteer(Guid ApplicantOwnerId, string AreasOfInterest);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class ApplyToVolunteerEndpoint
{
    public static ProblemDetails Validate(ApplyToVolunteer command, VolunteerApplication? volunteerApplication)
    {
        // The model's refusing scenarios arrange prior events, so these refusals are about
        // volunteerApplication's state, not the request's shape. Null means the stream does not exist yet.
        // TODO guard: return new ProblemDetails { Detail = "You have already applied to volunteer", Status = 400 };
        return WolverineContinue.NoProblems;
    }


    [WolverinePost("/api/volunteering/applytovolunteer")]
    [EmptyResponse]
    public static EventsToAppend Post(ApplyToVolunteer command, [WriteModel] VolunteerApplication? volunteerApplication)
    {
        // HOTSPOT (from the model): The board lists areas of interest as "Transport | Fundraising | Events | Administration | HomeChecks | FosterSupport" — which reads as a SET a person picks from, while its own sample row shows a single value. The curated format has no collection field, so this model carries one area and says so here rather than pretending. If it is really a set, it belongs on the event as a set and this slice changes shape.

        // HOTSPOT (from the model): One application per person, because the applicant's id IS the application's stream. Someone rejected in March and re-applying in September has nowhere to put the second application.

        // The decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        // A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;
        // Answering with a body instead of 204: drop [EmptyResponse], declare the response
        // record, and return it beside the events as a tuple.
        // Fill this in and delete the throw — the shape is:
        //     return [new VolunteerApplicationSubmitted(/* … */)];
        throw new NotImplementedException("TODO: ApplyToVolunteer — decide which events this slice appends");
    }

}


