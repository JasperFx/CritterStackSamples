using Bobcat;
using Bobcat.Xunit;

namespace BankAccountES.Tests;

/// <summary>
/// Five of the original tests, rewritten as Bobcat specifications. Each one arranges the aggregate's
/// history as events instead of through a chain of earlier requests, sends ONE request, and asserts
/// what it appended and what the aggregate became. The rest of the original tests are still in
/// <see cref="BankAccountTests" />, unchanged apart from the move to xUnit v3.
/// </summary>
[BobcatFeature("Bank Account Event Sourcing")]
public class BankAccountSpecs(BankAccountHost app) : BankAccountSpec(app)
{
    [BobcatSpec(typeof(EnrollClient))]
    public async Task enroll_a_client()
    {
        await WhenPosted(new EnrollClient("Alice Smith", "alice@test.com"), "/api/clients");

        ThenTheResponseStatusIs(200);
        ThenEvents(Specify<ClientEnrolled>().With(x => x.Name, "Alice Smith").With(x => x.Email, "alice@test.com"));
        await ThenAggregate<Client>(TheStartedStream<Client>(),
            Specify<Client>().With(x => x.Name, "Alice Smith").With(x => x.Email, "alice@test.com"));
    }

    [BobcatSpec(typeof(OpenAccount))]
    public async Task opening_an_account_for_an_unknown_client_is_refused()
    {
        // [Entity(Required = true)] answers 400 before the endpoint runs at all
        await WhenPosted(new OpenAccount(Guid.NewGuid()), "/api/accounts");

        ThenTheResponseStatusIs(400);
        ThenNoEvents();
    }

    [BobcatSpec(typeof(DepositFunds))]
    public async Task deposit_funds()
    {
        var accountId = Guid.NewGuid();
        await GivenEvents<Account>(accountId, new AccountOpened(accountId, Guid.NewGuid(), "USD"));

        await WhenPosted(new DepositFunds(accountId, 500m), $"/api/accounts/{accountId}/deposits");

        ThenTheResponseStatusIs(204);
        ThenEvents(new FundsDeposited(accountId, 500m, 500m));
        await ThenAggregate<Account>(accountId, Specify<Account>().With(x => x.Balance, 500m));
    }

    [BobcatSpec(typeof(WithdrawFunds))]
    public async Task withdrawing_more_than_the_balance_leaves_the_account_untouched()
    {
        var accountId = Guid.NewGuid();
        await GivenEvents<Account>(accountId,
            new AccountOpened(accountId, Guid.NewGuid(), "USD"),
            new FundsDeposited(accountId, 100m, 100m));

        await WhenPosted(new WithdrawFunds(accountId, 500m), $"/api/accounts/{accountId}/withdrawals");

        ThenTheResponseStatusIs(400);
        ThenNoEvents();
        await ThenAggregate<Account>(accountId, Specify<Account>().With(x => x.Balance, 100m));
    }

    [BobcatSpec]
    public async Task transaction_history_records_every_deposit_and_withdrawal()
    {
        var accountId = Guid.NewGuid();
        await GivenEvents<Account>(accountId,
            new AccountOpened(accountId, Guid.NewGuid(), "USD"),
            new FundsDeposited(accountId, 1000m, 1000m),
            new FundsWithdrawn(accountId, 200m, 800m));

        await WhenReading($"/api/accounts/{accountId}/transactions");

        ThenTheResponseStatusIs(200);
        var history = await TheResponse<AccountTransactions>();
        TheTransactionsAre(history, "Deposit 1000", "Withdrawal 200");
        TheBalanceIs(history, 800m);
    }

    [Then("the transactions are {expected}")]
    public void TheTransactionsAre(AccountTransactions history, params string[] expected)
        => SpecAssert.Check("transactions",
            string.Join(", ", history.Transactions.Select(x => $"{x.Type} {x.Amount:0.##}")),
            string.Join(", ", expected));

    [Then("the balance is {balance}")]
    public void TheBalanceIs(AccountTransactions history, decimal balance)
        => SpecAssert.Check("balance", history.Balance, balance);
}
