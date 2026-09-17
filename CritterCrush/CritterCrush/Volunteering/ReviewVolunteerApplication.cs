namespace CritterCrush.Volunteering;

/// <summary>An admin looked at the application</summary>
public record VolunteerApplicationReviewed(Guid ApplicantOwnerId);

public record ReviewVolunteerApplication([property: Identity] Guid ApplicantOwnerId);

/// <inheritdoc cref="CritterCrush.Scheduling.ConfirmAppointmentEndpoint"/>
public static class ReviewVolunteerApplicationEndpoint
{
    public static ProblemDetails Validate(VolunteerApplication volunteerApplication)
    {
        // Review is reachable only from Submitted. Stated as the state it requires: an exclusion
        // list would have to name Approved AND Rejected and would silently admit anything added
        // later.
        if (volunteerApplication.Status != VolunteerApplicationStatus.Submitted)
        {
            return new ProblemDetails { Detail = "This application has already been decided", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/reviewvolunteerapplication")]
    [EmptyResponse]
    public static EventsToAppend Post(ReviewVolunteerApplication command, [WriteModel] VolunteerApplication volunteerApplication) =>
        [new VolunteerApplicationReviewed(volunteerApplication.ApplicantOwnerId)];
}
