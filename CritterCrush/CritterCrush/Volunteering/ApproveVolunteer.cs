namespace CritterCrush.Volunteering;

public record ApproveVolunteer([property: Identity] Guid ApplicantOwnerId);

/// <inheritdoc cref="CritterCrush.Scheduling.ConfirmAppointmentEndpoint"/>
public static class ApproveVolunteerEndpoint
{
    public static ProblemDetails Validate(VolunteerApplication volunteerApplication)
    {
        if (volunteerApplication.Status != VolunteerApplicationStatus.Reviewed)
        {
            return new ProblemDetails { Detail = "This application has not been reviewed", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/approvevolunteer")]
    [EmptyResponse]
    public static EventsToAppend Post(ApproveVolunteer command, [WriteModel] VolunteerApplication volunteerApplication) =>
        [new VolunteerApproved(volunteerApplication.ApplicantOwnerId)];
}
