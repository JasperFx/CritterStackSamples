namespace CritterCrush.Volunteering;

public record ReviewVolunteerApplication([property: Identity] Guid ApplicantOwnerId);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class ReviewVolunteerApplicationEndpoint
{
    /// <summary>An application can be read until somebody decides it.</summary>
    public static ProblemDetails Validate(VolunteerApplication volunteerApplication)
        => VolunteerApplicationStatus.IsDecided(volunteerApplication.Status)
            ? new ProblemDetails { Detail = "This application has already been decided", Status = 400 }
            : WolverineContinue.NoProblems;


    [WolverinePost("/api/volunteering/reviewvolunteerapplication")]
    [EmptyResponse]
    public static VolunteerApplicationReviewed Post(ReviewVolunteerApplication command, [WriteModel] VolunteerApplication volunteerApplication)
        => new VolunteerApplicationReviewed(volunteerApplication.ApplicantOwnerId);

}
