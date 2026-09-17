namespace CritterCrush.Volunteering;

/// <inheritdoc cref="CritterCrush.Scheduling.AppointmentStatus"/>
public static class VolunteerApplicationStatus
{
    public const string Submitted = nameof(Submitted);
    public const string Reviewed = nameof(Reviewed);
    public const string Approved = nameof(Approved);
    public const string Rejected = nameof(Rejected);
}

public class VolunteerApplication
{
    public Guid Id { get; set; }
    public Guid ApplicantOwnerId { get; set; }
    public string AreasOfInterest { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public static VolunteerApplication Create(VolunteerApplicationSubmitted e) => new()
    {
        ApplicantOwnerId = e.ApplicantOwnerId,
        AreasOfInterest = e.AreasOfInterest,
        Status = VolunteerApplicationStatus.Submitted
    };

    public void Apply(VolunteerApplicationSubmitted e)
    {
        ApplicantOwnerId = e.ApplicantOwnerId;
        AreasOfInterest = e.AreasOfInterest;
        Status = VolunteerApplicationStatus.Submitted;
    }

    public void Apply(VolunteerApplicationReviewed e) => Status = VolunteerApplicationStatus.Reviewed;

    public void Apply(VolunteerApproved e) => Status = VolunteerApplicationStatus.Approved;

    public void Apply(VolunteerApplicationRejected e) => Status = VolunteerApplicationStatus.Rejected;
}
