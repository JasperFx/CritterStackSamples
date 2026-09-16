namespace CritterCrush.Volunteering;

/// <summary>An admin read the application and it now awaits a decision</summary>
public record VolunteerApplicationReviewed(Guid ApplicantOwnerId);

public record ReviewVolunteerApplication(Guid ApplicantOwnerId)
{
    [Identity] public Guid VolunteerApplicationId => ApplicantOwnerId;
}

public record ReviewVolunteerApplicationResponse();

/// <summary>Reviewing is not deciding — approval and rejection are their own slices.</summary>
public static class ReviewVolunteerApplicationEndpoint
{
    public static ProblemDetails Validate(ReviewVolunteerApplication command, [ReadModel] VolunteerApplication? volunteerApplication)
    {
        if (volunteerApplication is null) return VolunteeringRefusals.NoSuchApplication;
        if (volunteerApplication.IsDecided)
        {
            return new ProblemDetails { Detail = "This application has already been decided", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/reviewvolunteerapplication")]
    public static (ReviewVolunteerApplicationResponse, EventsToAppend) Post(ReviewVolunteerApplication command, [WriteModel] VolunteerApplication volunteerApplication) =>
        (new ReviewVolunteerApplicationResponse(), [new VolunteerApplicationReviewed(volunteerApplication.ApplicantOwnerId)]);
}
