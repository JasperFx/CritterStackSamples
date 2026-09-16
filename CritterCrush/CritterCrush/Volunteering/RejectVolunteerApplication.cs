namespace CritterCrush.Volunteering;

/// <summary>The applicant will not be volunteering</summary>
public record VolunteerApplicationRejected(Guid ApplicantOwnerId, string Reason);

public record RejectVolunteerApplication(Guid ApplicantOwnerId, string Reason)
{
    [Identity] public Guid VolunteerApplicationId => ApplicantOwnerId;
}

public record RejectVolunteerApplicationResponse();

public static class RejectVolunteerApplicationEndpoint
{
    public static ProblemDetails Validate(RejectVolunteerApplication command, [ReadModel] VolunteerApplication? volunteerApplication)
    {
        if (volunteerApplication is null) return VolunteeringRefusals.NoSuchApplication;
        if (volunteerApplication.IsDecided)
        {
            return new ProblemDetails { Detail = "This application has already been decided", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/rejectvolunteerapplication")]
    public static (RejectVolunteerApplicationResponse, EventsToAppend) Post(RejectVolunteerApplication command, [WriteModel] VolunteerApplication volunteerApplication) =>
        (new RejectVolunteerApplicationResponse(),
            [new VolunteerApplicationRejected(volunteerApplication.ApplicantOwnerId, command.Reason)]);
}
