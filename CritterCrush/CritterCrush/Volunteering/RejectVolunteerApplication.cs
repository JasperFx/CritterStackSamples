namespace CritterCrush.Volunteering;

public record RejectVolunteerApplication([property: Identity] Guid ApplicantOwnerId, string Reason);

/// <inheritdoc cref="CritterCrush.Scheduling.ConfirmAppointmentEndpoint"/>
public static class RejectVolunteerApplicationEndpoint
{
    public static ProblemDetails Validate(VolunteerApplication volunteerApplication)
    {
        // Rejection sits opposite ApproveVolunteer and requires the same state: an application a
        // human has actually looked at.
        if (volunteerApplication.Status != VolunteerApplicationStatus.Reviewed)
        {
            return new ProblemDetails { Detail = "This application has already been decided", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/rejectvolunteerapplication")]
    [EmptyResponse]
    public static EventsToAppend Post(RejectVolunteerApplication command, [WriteModel] VolunteerApplication volunteerApplication) =>
        [new VolunteerApplicationRejected(volunteerApplication.ApplicantOwnerId, command.Reason)];
}
