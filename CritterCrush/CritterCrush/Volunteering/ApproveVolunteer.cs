namespace CritterCrush.Volunteering;

public record ApproveVolunteer([property: Identity] Guid ApplicantOwnerId);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class ApproveVolunteerEndpoint
{
    /// <summary>Approval follows a review — the state it requires, not the ones it excludes.</summary>
    public static ProblemDetails Validate(VolunteerApplication volunteerApplication)
        => volunteerApplication.Status == VolunteerApplicationStatus.Reviewed
            ? WolverineContinue.NoProblems
            : new ProblemDetails { Detail = "This application has not been reviewed", Status = 400 };


    [WolverinePost("/api/volunteering/approvevolunteer")]
    [EmptyResponse]
    public static VolunteerApproved Post(ApproveVolunteer command, [WriteModel] VolunteerApplication volunteerApplication)
        => new VolunteerApproved(volunteerApplication.ApplicantOwnerId);

}
