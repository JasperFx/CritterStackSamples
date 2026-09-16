namespace CritterCrush.Scheduling;

/// <summary>
/// The one refusal every state-dependent slice shares. `[ReadModel] Appointment?` is null when the
/// stream does not exist, and a POST naming an appointment that was never proposed is a 404, not a
/// 400 — the request is well formed, the appointment simply is not there. The model does not
/// scenario it, because a scenario would have to arrange an absence; the six endpoints would
/// otherwise each carry their own copy of this.
/// </summary>
internal static class Refusals
{
    internal static ProblemDetails NoSuchAppointment => new() { Detail = "No such appointment", Status = 404 };
}
