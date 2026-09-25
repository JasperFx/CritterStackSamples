namespace CritterCrush.Volunteering;

public record ApplyToVolunteer([property: Identity] Guid ApplicantOwnerId, string AreasOfInterest);

/// <summary>
/// The endpoint IS the handler: one transaction, honest status codes. Split a separate
/// message handler out only when this command genuinely needs bus visibility — other
/// callers, retry policies, scheduling — never for testability.
/// </summary>
public static class ApplyToVolunteerEndpoint
{
    /// <summary>
    /// One application per person: the applicant's id IS the application's stream, so a second
    /// application has nowhere to live. The model flags that as a hotspot; this code states it.
    /// </summary>
    public static ProblemDetails Validate(VolunteerApplication? volunteerApplication)
        => volunteerApplication is null
            ? WolverineContinue.NoProblems
            : new ProblemDetails { Detail = "You have already applied to volunteer", Status = 400 };


    [WolverinePost("/api/volunteering/applytovolunteer")]
    [EmptyResponse]
    public static VolunteerApplicationSubmitted Post(ApplyToVolunteer command, [WriteModel] VolunteerApplication? volunteerApplication)
        => new VolunteerApplicationSubmitted(command.ApplicantOwnerId, command.AreasOfInterest);

}
