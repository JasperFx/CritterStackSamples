# Bank Account (Event Sourcing) — Critter Stack Sample

## Domain Reference

**Inspired by:** [andreschaffer/event-sourcing-cqrs-examples](https://github.com/andreschaffer/event-sourcing-cqrs-examples) (Java)

A minimalistic bank domain demonstrating Marten event sourcing with Wolverine aggregate handlers. Not a code port — built from scratch using the Java project's domain concepts.

## Domain

- **Client** — enroll, update profile (event-sourced)
- **Account** — open, deposit, withdraw (event-sourced, with balance guard)
- **Transaction History** — inline projection built from deposit/withdrawal events

## Patterns Demonstrated

### Aggregate Handler Workflow (`[AggregateHandler]`)

Deposit and withdrawal operations use Wolverine's `[AggregateHandler]` attribute. Wolverine loads the Account aggregate from the event stream, passes it to the handler, appends the returned event, and commits — all in one transaction.

```csharp
[WolverinePost("/api/accounts/{accountId}/deposits")]
[AggregateHandler]
public static FundsDeposited Post(DepositFunds command, Account account)
{
    var newBalance = account.Balance + command.Amount;
    return new FundsDeposited(command.AccountId, command.Amount, newBalance);
}
```

### Validate Against Aggregate State

Withdrawal validates against the loaded aggregate's balance using a separate `Validate` method (Railway Programming pattern):

```csharp
public static ProblemDetails Validate(WithdrawFunds command, Account account)
{
    if (account.Balance < command.Amount)
        return new ProblemDetails { Detail = "Insufficient funds", Status = 400 };
    return WolverineContinue.NoProblems;
}
```

### Inline Snapshot Projections

Account and Client aggregates use `SnapshotLifecycle.Inline` — the snapshot document is always up-to-date after each event append.

### Single-Stream Read Model Projection

`AccountTransactionsProjection` builds a transaction history per account from deposit/withdrawal events, registered as an inline projection.

### Entity Loading with Batch Query

`OpenAccount` uses `[Entity]` to load the Client by `ClientId` — verifying the client exists before opening an account.

## Running

Requires PostgreSQL.

```bash
dotnet run
```

Swagger UI at `/swagger`.

## API

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/clients` | Enroll a new client |
| PUT | `/api/clients/{clientId}` | Update client profile |
| GET | `/api/clients/{id}` | Get client |
| POST | `/api/accounts` | Open account for a client |
| GET | `/api/accounts/{id}` | Get account |
| GET | `/api/clients/{clientId}/accounts` | Get all accounts for a client |
| POST | `/api/accounts/{accountId}/deposits` | Deposit funds |
| POST | `/api/accounts/{accountId}/withdrawals` | Withdraw funds |
| GET | `/api/accounts/{accountId}/transactions` | Transaction history |
