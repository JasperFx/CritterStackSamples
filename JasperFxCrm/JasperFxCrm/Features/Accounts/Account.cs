namespace JasperFxCrm.Features.Accounts;

// The Account and Contact streams are ported from the portal in a later chapter with their
// event shapes unchanged, because the production data is already in this shape. Leads needs
// the two creation events today, for the "is this an existing customer" decision.

public enum AccountKind
{
    Individual,
    Organization
}

public enum AccountSource
{
    Checkout,
    AdminGrant,
    Trial,
    LeadConversion
}

public enum ContactRole
{
    Member,
    Admin
}

public sealed record AccountCreated(
    Guid AccountId,
    string PrimaryEmail,
    string? DomainHint,
    AccountSource Source,
    AccountKind Kind = AccountKind.Individual,
    string? Name = null);

public sealed record ContactCreated(
    Guid ContactId,
    Guid AccountId,
    string Email,
    string? Name = null,
    bool IsPrimary = false,
    ContactRole Role = ContactRole.Member);

public sealed class Account
{
    public Guid Id { get; set; }
    public string PrimaryEmail { get; set; } = "";
    public string? DomainHint { get; set; }
    public AccountKind Kind { get; set; }
    public string? Name { get; set; }

    public static Account Create(AccountCreated e) => new()
    {
        Id = e.AccountId, PrimaryEmail = e.PrimaryEmail, DomainHint = e.DomainHint, Kind = e.Kind, Name = e.Name
    };
}

public sealed class Contact
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public bool IsPrimary { get; set; }
    public ContactRole Role { get; set; }

    public static Contact Create(ContactCreated e) => new()
    {
        Id = e.ContactId, AccountId = e.AccountId, Email = e.Email, Name = e.Name, IsPrimary = e.IsPrimary, Role = e.Role
    };
}
