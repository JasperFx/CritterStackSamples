namespace JasperFxCrm.Infrastructure.Identity;

/// <summary>
/// Email and company-domain normalization shared by Leads, Trials and Contacts.
/// Pure, so the rules are unit-testable without a database.
/// </summary>
public static class EmailIdentity
{
    /// <summary>
    /// Personal mailbox providers. A submission from one of these identifies only the
    /// person, never a company, or everyone on Gmail would be one lead.
    /// </summary>
    private static readonly HashSet<string> FreeMailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com",
        "outlook.com", "hotmail.com", "hotmail.co.uk", "live.com", "msn.com",
        "yahoo.com", "yahoo.co.uk", "yahoo.co.in", "ymail.com", "rocketmail.com",
        "icloud.com", "me.com", "mac.com",
        "aol.com",
        "proton.me", "protonmail.com", "pm.me",
        "gmx.com", "gmx.de", "gmx.net", "web.de", "mail.com",
        "zoho.com", "zohomail.com",
        "yandex.com", "yandex.ru", "mail.ru",
        "qq.com", "163.com", "126.com",
        "fastmail.com", "hey.com", "tutanota.com", "tuta.io",
        "rediffmail.com",
    };

    /// <summary>
    /// Lowercased, trimmed, with any <c>+tag</c> removed from the local part, so
    /// <c>Jane+crm@Acme.com</c> and <c>jane@acme.com</c> are one person. Gmail also
    /// ignores dots in the local part and treats googlemail.com as gmail.com.
    /// </summary>
    public static string Normalize(string email)
    {
        var lowered = email.Trim().ToLowerInvariant();
        var at = lowered.LastIndexOf('@');
        if (at <= 0) return lowered;

        var local = lowered[..at];
        var domain = lowered[(at + 1)..];
        var plus = local.IndexOf('+');
        if (plus > 0) local = local[..plus];

        if (domain is "gmail.com" or "googlemail.com")
        {
            var undotted = local.Replace(".", "");
            if (undotted.Length > 0) local = undotted;
            domain = "gmail.com";
        }

        return $"{local}@{domain}";
    }

    /// <summary>The email's domain, lowercased; null when the email has none.</summary>
    public static string? Domain(string email)
    {
        var lowered = email.Trim().ToLowerInvariant();
        var at = lowered.LastIndexOf('@');
        return at > 0 && at < lowered.Length - 1 ? lowered[(at + 1)..] : null;
    }

    public static bool IsFreeMail(string domain) => FreeMailDomains.Contains(domain.Trim());

    /// <summary>The email's domain when it identifies a company; null for free-mail.</summary>
    public static string? CompanyDomain(string email) =>
        Domain(email) is { } domain && !IsFreeMail(domain) ? domain : null;
}
