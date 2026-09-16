namespace CritterCrush.Volunteering;

/// <summary>Somebody asked to volunteer</summary>
public record VolunteerApplicationSubmitted(Guid ApplicantOwnerId, string AreasOfInterest);

/// <summary>
/// The applicant IS the application: one person, one application, so the stream id is the
/// applicant's own id rather than a second identifier nobody has. `[Identity]` is how a request
/// says that — without it Wolverine cannot resolve the aggregate and refuses the DISPATCH, which
/// takes the whole host down with it, because HTTP endpoints are discovered eagerly.
/// </summary>
public record ApplyToVolunteer(Guid ApplicantOwnerId, string AreasOfInterest)
{
    [Identity] public Guid VolunteerApplicationId => ApplicantOwnerId;
}

public record ApplyToVolunteerResponse();

public static class ApplyToVolunteerEndpoint
{
    public static ProblemDetails Validate(ApplyToVolunteer command, [ReadModel] VolunteerApplication? volunteerApplication)
    {
        // Null is the EXPECTED state here — this command creates the stream. A non-null aggregate
        // is the refusal.
        if (volunteerApplication is not null)
        {
            return new ProblemDetails { Detail = "You have already applied to volunteer", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/applytovolunteer")]
    public static (ApplyToVolunteerResponse, EventsToAppend) Post(ApplyToVolunteer command, [WriteModel] VolunteerApplication? volunteerApplication) =>
        (new ApplyToVolunteerResponse(),
            [new VolunteerApplicationSubmitted(command.ApplicantOwnerId, command.AreasOfInterest)]);
}
