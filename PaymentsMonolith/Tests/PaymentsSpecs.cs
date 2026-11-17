using Bobcat;
using Bobcat.Xunit;
using Customers;
using PaymentsMonolith;
using Shouldly;
using Users;
using Wallets;

namespace Tests;

/// <summary>
/// Five of the original tests, rewritten as Bobcat specifications. The originals reached each state
/// through a chain of earlier requests with <c>Task.Delay(1000)</c> between them, waiting for the
/// cascaded integration events to be handled on the durable local queues. Here each spec arranges the
/// documents it needs, sends ONE request inside a tracked session, which settles every cascade before
/// the Thens run, and asserts the integration events and the documents downstream modules stored.
/// </summary>
[BobcatFeature("Users")]
public class UserSpecs(PaymentsHost app) : PaymentsSpec(app)
{
    [BobcatSpec(typeof(RegisterUser))]
    public async Task registering_a_user_creates_the_customer_stub()
    {
        await WhenPosted(new RegisterUser("alice@example.com", "Alice Smith", "password123"), "/api/users");

        ThenTheResponseStatusIs(200);
        await ThenSingleDocument<User>(Specify<User>().With(x => x.Email, "alice@example.com").With(x => x.FullName, "Alice Smith"));
        ThenMessageSent(Specify<UserCreated>().With(x => x.Email, "alice@example.com").With(x => x.FullName, "Alice Smith"));

        // The Customers module handled UserCreated: no wait needed
        await ThenSingleDocument<Customer>(Specify<Customer>()
            .With(x => x.Email, "alice@example.com")
            .With(x => x.IsCompleted, false));
    }
}

[BobcatFeature("Customers")]
public class CustomerSpecs(PaymentsHost app) : PaymentsSpec(app)
{
    [BobcatSpec(typeof(CompleteCustomer))]
    public async Task completing_a_customer_opens_their_wallet()
    {
        var id = Guid.NewGuid();
        await GivenReadModel<Customer>(new Customer { Id = id, Email = "bob@example.com", FullName = "Bob Jones" });

        await WhenPut(new CompleteCustomer(id, "Bob", "Bob Jones", "US"), $"/api/customers/{id}/complete");

        ThenTheResponseStatusIs(200);
        await ThenDocument<Customer>(id, Specify<Customer>()
            .With(x => x.IsCompleted, true)
            .With(x => x.Name, "Bob")
            .With(x => x.Nationality, "US"));
        ThenMessageSent(new CustomerCompleted(id, "Bob", "Bob Jones", "US"));

        // The Wallets module handled CustomerCompleted
        await ThenSingleDocument<Wallet>(Specify<Wallet>()
            .With(x => x.OwnerId, id)
            .With(x => x.Currency, "PLN")
            .With(x => x.Balance, 0m));
        ThenMessageSent(Specify<WalletCreated>().With(x => x.OwnerId, id));
    }
}

[BobcatFeature("Wallets")]
public class WalletSpecs(PaymentsHost app) : PaymentsSpec(app)
{
    [BobcatSpec(typeof(AddFunds))]
    public async Task adding_funds_credits_the_wallet()
    {
        var wallet = AWallet();
        await GivenReadModel<Wallet>(wallet);

        await WhenPosted(new AddFunds(wallet.Id, 500m, "initial deposit"), $"/api/wallets/{wallet.Id}/funds/add");

        ThenTheResponseStatusIs(200);
        await ThenDocument<Wallet>(wallet.Id, Specify<Wallet>(
            // An expression tree cannot use optional arguments (CS0854), so Shouldly's customMessage is passed
            x => x.Balance.ShouldBe(500m, null),
            x => x.Transfers.ShouldHaveSingleItem(null)));
        ThenMessageSent(new FundsAdded(wallet.Id, wallet.OwnerId, "PLN", 500m));
    }

    [BobcatSpec(typeof(TransferFunds))]
    public async Task transferring_funds_moves_the_balance()
    {
        var from = AWallet(balance: 1000m);
        var to = AWallet();
        await GivenReadModel<Wallet>(from);
        await GivenReadModel<Wallet>(to);

        await WhenPosted(new TransferFunds(from.Id, to.Id, 300m), "/api/wallets/transfer");

        ThenTheResponseStatusIs(200);
        await ThenDocument<Wallet>(from.Id, Specify<Wallet>().With(x => x.Balance, 700m));
        await ThenDocument<Wallet>(to.Id, Specify<Wallet>().With(x => x.Balance, 300m));
        ThenMessageSent(new FundsDeducted(from.Id, from.OwnerId, "PLN", 300m));
        ThenMessageSent(new FundsAdded(to.Id, to.OwnerId, "PLN", 300m));
    }

    [BobcatSpec(typeof(TransferFunds))]
    public async Task transferring_more_than_the_balance_is_refused()
    {
        var from = AWallet();
        var to = AWallet();
        await GivenReadModel<Wallet>(from);
        await GivenReadModel<Wallet>(to);

        await WhenPosted(new TransferFunds(from.Id, to.Id, 100m), "/api/wallets/transfer");

        ThenTheResponseStatusIs(400);
        ThenNoMessageSent<FundsDeducted>();
        await ThenDocument<Wallet>(from.Id, Specify<Wallet>().With(x => x.Balance, 0m));
        await ThenDocument<Wallet>(to.Id, Specify<Wallet>().With(x => x.Balance, 0m));
    }

    private static Wallet AWallet(decimal balance = 0m) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        OwnerName = "Owner",
        Currency = "PLN",
        Balance = balance,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
