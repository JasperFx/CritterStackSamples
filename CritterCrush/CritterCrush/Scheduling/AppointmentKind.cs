namespace CritterCrush.Scheduling;

/// <summary>
/// What a proposed appointment is FOR. Three chapters propose appointments; the kind is how the
/// queue and the owner's page tell them apart. <c>string</c> for the reason
/// <see cref="AppointmentStatus"/> is.
/// </summary>
public static class AppointmentKind
{
    public const string HomeCheck = nameof(HomeCheck);
    public const string FosterHandover = nameof(FosterHandover);
    public const string SurrenderIntake = nameof(SurrenderIntake);
}
