namespace CritterCrush.Volunteering;

/// <summary>The volunteer visited and wrote it up</summary>
public record HomeCheckReportSubmitted(
    Guid ApplicationId,
    Guid OwnerId,
    Guid ShelterId,
    string Outcome,
    string Notes);

public record SubmitHomeCheckReport(Guid HomeCheckId, string Outcome, string Notes);

public record SubmitHomeCheckReportResponse();

/// <summary>
/// The report leaves this boundary: the board's note says its only real consumer is
/// ShelterReviewsApplication, and advisory there. No slice in this model handles it, and the model
/// says so with an outbound external-system edge rather than leaving it dangling.
/// </summary>
public static class SubmitHomeCheckReportEndpoint
{
    public static ProblemDetails Validate(SubmitHomeCheckReport command, [ReadModel] HomeCheck? homeCheck)
    {
        if (homeCheck is null) return VolunteeringRefusals.NoSuchHomeCheck;
        if (homeCheck.Status != HomeCheckStatus.Assigned)
        {
            return new ProblemDetails { Detail = "Nobody has accepted this home check", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/submithomecheckreport")]
    public static (SubmitHomeCheckReportResponse, EventsToAppend) Post(SubmitHomeCheckReport command, [WriteModel] HomeCheck homeCheck) =>
        (new SubmitHomeCheckReportResponse(),
            [
                new HomeCheckReportSubmitted(
                    homeCheck.ApplicationId,
                    homeCheck.OwnerId,
                    homeCheck.ShelterId,
                    command.Outcome,
                    command.Notes)
            ]);
}
