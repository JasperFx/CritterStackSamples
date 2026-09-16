namespace CritterCrush.Volunteering;

/// <summary>The applicant may now be assigned home checks</summary>
public record VolunteerApproved(Guid ApplicantOwnerId);

public record ApproveVolunteer(Guid ApplicantOwnerId)
{
    [Identity] public Guid VolunteerApplicationId => ApplicantOwnerId;
}

public record ApproveVolunteerResponse();

/// <summary>
/// Approval marks the person eligible to be assigned home checks. The board's restraint, quoted in
/// the model: a Volunteer is a Member playing a role, granted by a cross-module event rather than
/// by a general AssignRole endpoint.
/// </summary>
public static class ApproveVolunteerEndpoint
{
    public static ProblemDetails Validate(ApproveVolunteer command, [ReadModel] VolunteerApplication? volunteerApplication)
    {
        if (volunteerApplication is null) return VolunteeringRefusals.NoSuchApplication;
        if (volunteerApplication.Status != VolunteerApplicationStatus.Reviewed)
        {
            return new ProblemDetails { Detail = "This application has not been reviewed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/approvevolunteer")]
    public static (ApproveVolunteerResponse, EventsToAppend) Post(ApproveVolunteer command, [WriteModel] VolunteerApplication volunteerApplication) =>
        (new ApproveVolunteerResponse(), [new VolunteerApproved(volunteerApplication.ApplicantOwnerId)]);
}
