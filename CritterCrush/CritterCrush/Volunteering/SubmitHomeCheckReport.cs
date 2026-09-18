namespace CritterCrush.Volunteering;

public record SubmitHomeCheckReport([property: Identity] Guid HomeCheckId, string Outcome, string Notes);

/// <inheritdoc cref="CritterCrush.Scheduling.ConfirmAppointmentEndpoint"/>
public static class SubmitHomeCheckReportEndpoint
{
    public static ProblemDetails Validate(HomeCheck homeCheck)
    {
        // Only somebody who accepted the visit can report on it.
        if (homeCheck.Status != HomeCheckStatus.Accepted)
        {
            return new ProblemDetails { Detail = "Nobody has accepted this home check", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/submithomecheckreport")]
    [EmptyResponse]
    public static EventsToAppend Post(SubmitHomeCheckReport command, [WriteModel] HomeCheck homeCheck) =>
        [new HomeCheckReportSubmitted(homeCheck.ApplicationId, homeCheck.OwnerId, homeCheck.ShelterId, command.Outcome, command.Notes)];
}
