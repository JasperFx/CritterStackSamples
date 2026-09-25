namespace CritterCrush.Volunteering;

/// <summary>
/// Where an application is in its life. <c>string</c> for the same reason the model states it as
/// one: the curated format has no enum, so the domain follows the design record.
/// </summary>
public static class VolunteerApplicationStatus
{
    public const string Submitted = nameof(Submitted);
    public const string Reviewed = nameof(Reviewed);
    public const string Approved = nameof(Approved);
    public const string Rejected = nameof(Rejected);

    /// <summary>The two terminal states. Both guards that say "already been decided" mean this.</summary>
    public static bool IsDecided(string status) => status is Approved or Rejected;
}

public class VolunteerApplication
{
    public Guid Id { get; set; }
    public Guid ApplicantOwnerId { get; set; }
    public string AreasOfInterest { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public static VolunteerApplication Create(VolunteerApplicationSubmitted submitted) => new()
    {
        ApplicantOwnerId = submitted.ApplicantOwnerId,
        AreasOfInterest = submitted.AreasOfInterest,
        Status = VolunteerApplicationStatus.Submitted
    };


    public void Apply(VolunteerApplicationReviewed _) => Status = VolunteerApplicationStatus.Reviewed;


    public void Apply(VolunteerApproved _) => Status = VolunteerApplicationStatus.Approved;


    public void Apply(VolunteerApplicationRejected _) => Status = VolunteerApplicationStatus.Rejected;

}
