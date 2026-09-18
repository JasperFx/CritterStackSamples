namespace CritterCrush.Volunteering;

public record RequestHomeCheck([property: Identity] Guid HomeCheckId, Guid ApplicationId, Guid OwnerId, Guid ShelterId);

/// <inheritdoc cref="CritterCrush.Scheduling.ConfirmAppointmentEndpoint"/>
public static class RequestHomeCheckEndpoint
{
    public static ProblemDetails Validate(HomeCheck? homeCheck)
    {
        // The creating slice: null is the expected state, non-null is the refusal. Live, unlike the
        // null check under a non-nullable write model.
        if (homeCheck is not null)
        {
            return new ProblemDetails { Detail = "This home check has already been requested", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/requesthomecheck")]
    [EmptyResponse]
    public static EventsToAppend Post(RequestHomeCheck command, [WriteModel] HomeCheck? homeCheck) =>
        [new HomeCheckRequested(command.ApplicationId, command.OwnerId, command.ShelterId)];
}
