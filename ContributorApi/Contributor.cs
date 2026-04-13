namespace ContributorApi;

/// <summary>
/// Contributor document stored in Marten.
/// Replaces: Contributor aggregate + ContributorId/ContributorName/PhoneNumber value objects
/// + ContributorStatus smart enum + Vogen code generation + EF Core configurations.
/// </summary>
public class Contributor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "NotSet"; // CoreTeam, Community, NotSet
    public PhoneNumber? PhoneNumber { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class PhoneNumber
{
    public string CountryCode { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string? Extension { get; set; }

    public override string ToString()
        => string.IsNullOrEmpty(Extension)
            ? $"+{CountryCode} {Number}"
            : $"+{CountryCode} {Number} x{Extension}";
}
