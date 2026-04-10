namespace BankAccountES;

// --- Domain Events ---

public record AccountOpened(Guid AccountId, Guid ClientId, string Currency);
public record FundsDeposited(Guid AccountId, decimal Amount, decimal NewBalance);
public record FundsWithdrawn(Guid AccountId, decimal Amount, decimal NewBalance);

// --- Aggregate (event-sourced via Marten) ---

/// <summary>
/// Bank account aggregate. Marten rebuilds state by calling Apply methods
/// when loading from the event stream. Business rules are enforced in
/// Wolverine handler methods that return events.
/// </summary>
public class Account
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal Balance { get; set; }

    public void Apply(AccountOpened e)
    {
        Id = e.AccountId;
        ClientId = e.ClientId;
        Currency = e.Currency;
    }

    public void Apply(FundsDeposited e)
    {
        Balance = e.NewBalance;
    }

    public void Apply(FundsWithdrawn e)
    {
        Balance = e.NewBalance;
    }
}
