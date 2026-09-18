namespace CritterCrush.Volunteering;

/// <summary>
/// ApplicantOwnerId doubles as the application's stream identity — one person, one application —
/// which is what makes "applying twice" a state question rather than a query.
/// </summary>
public record ApplyToVolunteer([property: Identity] Guid ApplicantOwnerId, string AreasOfInterest);

/// <inheritdoc cref="CritterCrush.Scheduling.ConfirmAppointmentEndpoint"/>
public static class ApplyToVolunteerEndpoint
{
    public static ProblemDetails Validate(VolunteerApplication? volunteerApplication)
    {
        // The creating slice, so the nullable write model is the guard: here null is the expected
        // state and non-null is the refusal. This is the one shape where a hand-written null check
        // is live rather than unreachable.
        if (volunteerApplication is not null)
        {
            return new ProblemDetails { Detail = "You have already applied to volunteer", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/volunteering/applytovolunteer")]
    [EmptyResponse]
    public static EventsToAppend Post(ApplyToVolunteer command, [WriteModel] VolunteerApplication? volunteerApplication) =>
        [new VolunteerApplicationSubmitted(command.ApplicantOwnerId, command.AreasOfInterest)];
}
