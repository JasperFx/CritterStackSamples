namespace CritterCrush.Volunteering;

public record RejectVolunteerApplication([property: Identity] Guid ApplicantOwnerId, string Reason);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class RejectVolunteerApplicationEndpoint
{
    /// <summary>A decision is made once.</summary>
    public static ProblemDetails Validate(VolunteerApplication volunteerApplication)
        => VolunteerApplicationStatus.IsDecided(volunteerApplication.Status)
            ? new ProblemDetails { Detail = "This application has already been decided", Status = 400 }
            : WolverineContinue.NoProblems;


    [WolverinePost("/api/volunteering/rejectvolunteerapplication")]
    [EmptyResponse]
    public static VolunteerApplicationRejected Post(RejectVolunteerApplication command, [WriteModel] VolunteerApplication volunteerApplication)
        => new VolunteerApplicationRejected(volunteerApplication.ApplicantOwnerId, command.Reason);

}
