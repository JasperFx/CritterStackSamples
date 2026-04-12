using Marten;
using Marten.Events.Aggregation;
using Wolverine.Http;

namespace BankAccountES;

/// <summary>
/// Read model projection: builds a transaction history per account
/// from deposit and withdrawal events. Replaces the Java sample's
/// commutative TransactionProjection that used version-based ordering.
///
/// In Marten, event ordering is guaranteed by the event store's
/// sequence numbers — no manual version tracking needed.
/// </summary>
public class AccountTransactions
{
    public Guid Id { get; set; } // AccountId
    public List<Transaction> Transactions { get; set; } = [];
    public decimal Balance { get; set; }
}

public class Transaction
{
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Single-stream projection: one AccountTransactions document per account stream.
/// Registered as Inline so the read model is always up-to-date.
/// </summary>
public class AccountTransactionsProjection : SingleStreamProjection<AccountTransactions, Guid>
{
    public AccountTransactionsProjection()
    {
        // Tells Marten to create the document on the first event
        CreateEvent<AccountOpened>(e => new AccountTransactions { Id = e.AccountId });
    }

    public void Apply(FundsDeposited e, AccountTransactions view)
    {
        view.Balance = e.NewBalance;
        view.Transactions.Add(new Transaction
        {
            Type = "Deposit",
            Amount = e.Amount,
            BalanceAfter = e.NewBalance,
            Timestamp = DateTimeOffset.UtcNow,
        });
    }

    public void Apply(FundsWithdrawn e, AccountTransactions view)
    {
        view.Balance = e.NewBalance;
        view.Transactions.Add(new Transaction
        {
            Type = "Withdrawal",
            Amount = e.Amount,
            BalanceAfter = e.NewBalance,
            Timestamp = DateTimeOffset.UtcNow,
        });
    }
}

// --- Query endpoints ---

public static class GetTransactionsEndpoint
{
    [WolverineGet("/api/accounts/{accountId}/transactions")]
    public static async Task<AccountTransactions?> Get(Guid accountId, IQuerySession session, CancellationToken ct)
        => await session.LoadAsync<AccountTransactions>(accountId, ct);
}

public static class GetAccountEndpoint
{
    [WolverineGet("/api/accounts/{id}")]
    public static async Task<Account?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => await session.Events.AggregateStreamAsync<Account>(id, token: ct);
}

public static class GetClientEndpoint
{
    [WolverineGet("/api/clients/{id}")]
    public static async Task<Client?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => await session.Events.AggregateStreamAsync<Client>(id, token: ct);
}

public static class GetClientAccountsEndpoint
{
    [WolverineGet("/api/clients/{clientId}/accounts")]
    public static Task<IReadOnlyList<Account>> Get(Guid clientId, IQuerySession session, CancellationToken ct)
        => session.Query<Account>().Where(a => a.ClientId == clientId).ToListAsync(ct);
}
