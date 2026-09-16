namespace CritterCrush.Volunteering;

/// <summary>
/// The refusal the state-dependent volunteering slices share. A POST naming an application or a
/// home check that was never created is a 404 — the request is well formed, the thing is not there.
/// No scenario arranges it, because a scenario would have to arrange an absence.
/// </summary>
internal static class VolunteeringRefusals
{
    internal static ProblemDetails NoSuchApplication => new() { Detail = "No such volunteer application", Status = 404 };

    internal static ProblemDetails NoSuchHomeCheck => new() { Detail = "No such home check", Status = 404 };
}
