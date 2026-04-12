using Alba;
using Customers;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Payments;
using PaymentsMonolith;
using Shouldly;
using Users;
using Wallets;

namespace Tests;

public class PaymentsTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    #region Users

    [Fact]
    public async Task Can_register_user()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser(
                "alice@example.com",
                "Alice Smith",
                "password123"
            )).ToUrl("/api/users");

            x.StatusCodeShouldBe(200);
        });

        var user = result.ReadAsJson<User>();
        user.ShouldNotBeNull();
        user!.Id.ShouldNotBe(Guid.Empty);
        user.Email.ShouldBe("alice@example.com");
        user.FullName.ShouldBe("Alice Smith");
    }

    [Fact]
    public async Task Register_user_validates_input()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("", "", "short")).ToUrl("/api/users");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Register_user_rejects_duplicate_email()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("dup@example.com", "First User", "password123"))
                .ToUrl("/api/users");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("dup@example.com", "Second User", "password456"))
                .ToUrl("/api/users");
            x.StatusCodeShouldBe(409);
        });
    }

    #endregion

    #region Customers

    [Fact]
    public async Task Can_complete_customer()
    {
        // Register user first — the cascading handler creates a Customer stub
        var user = (await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("bob@example.com", "Bob Jones", "password123"))
                .ToUrl("/api/users");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<User>()!;

        // Wait for the cascading UserCreated handler to create the Customer
        await Task.Delay(1000);

        // Complete customer
        var result = await _host.Scenario(x =>
        {
            x.Put.Json(new CompleteCustomer(
                user.Id,
                "Bob",
                "Bob Jones",
                "US"
            )).ToUrl($"/api/customers/{user.Id}/complete");

            x.StatusCodeShouldBe(200);
        });

        var customer = result.ReadAsJson<Customer>();
        customer.ShouldNotBeNull();
        customer!.IsCompleted.ShouldBeTrue();
        customer.Name.ShouldBe("Bob");
        customer.Nationality.ShouldBe("US");
    }

    #endregion

    #region Wallets

    [Fact]
    public async Task Can_add_funds_to_wallet()
    {
        // Full flow: register user -> complete customer -> wallet auto-created -> add funds
        var user = (await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("funds@example.com", "Funds User", "password123"))
                .ToUrl("/api/users");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<User>()!;

        await Task.Delay(1000);

        await _host.Scenario(x =>
        {
            x.Put.Json(new CompleteCustomer(user.Id, "Funds", "Funds User", "PL"))
                .ToUrl($"/api/customers/{user.Id}/complete");
            x.StatusCodeShouldBe(200);
        });

        // Wait for CustomerCompleted handler to create wallet
        await Task.Delay(1000);

        // Get wallet by owner
        var walletsResult = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/wallets/owner/{user.Id}");
            x.StatusCodeShouldBe(200);
        });

        var wallets = walletsResult.ReadAsJson<List<Wallet>>();
        wallets.ShouldNotBeNull();
        wallets!.ShouldNotBeEmpty();

        var wallet = wallets[0];

        // Add funds
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new AddFunds(wallet.Id, 500m, "initial deposit"))
                .ToUrl($"/api/wallets/{wallet.Id}/funds/add");
            x.StatusCodeShouldBe(200);
        });

        var updated = result.ReadAsJson<Wallet>();
        updated.ShouldNotBeNull();
        updated!.Balance.ShouldBe(500m);
        updated.Transfers.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Can_transfer_funds()
    {
        // Create two users with wallets
        var user1 = (await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("xfer1@example.com", "Transfer User 1", "password123"))
                .ToUrl("/api/users");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<User>()!;

        var user2 = (await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("xfer2@example.com", "Transfer User 2", "password123"))
                .ToUrl("/api/users");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<User>()!;

        await Task.Delay(1000);

        await _host.Scenario(x =>
        {
            x.Put.Json(new CompleteCustomer(user1.Id, "User1", "Transfer User 1", "PL"))
                .ToUrl($"/api/customers/{user1.Id}/complete");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Put.Json(new CompleteCustomer(user2.Id, "User2", "Transfer User 2", "PL"))
                .ToUrl($"/api/customers/{user2.Id}/complete");
            x.StatusCodeShouldBe(200);
        });

        await Task.Delay(1000);

        // Get wallets
        var wallets1 = (await _host.Scenario(x =>
        {
            x.Get.Url($"/api/wallets/owner/{user1.Id}");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<List<Wallet>>()!;

        var wallets2 = (await _host.Scenario(x =>
        {
            x.Get.Url($"/api/wallets/owner/{user2.Id}");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<List<Wallet>>()!;

        var wallet1 = wallets1[0];
        var wallet2 = wallets2[0];

        // Add funds to wallet1
        await _host.Scenario(x =>
        {
            x.Post.Json(new AddFunds(wallet1.Id, 1000m))
                .ToUrl($"/api/wallets/{wallet1.Id}/funds/add");
            x.StatusCodeShouldBe(200);
        });

        // Transfer from wallet1 to wallet2
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new TransferFunds(wallet1.Id, wallet2.Id, 300m))
                .ToUrl("/api/wallets/transfer");
            x.StatusCodeShouldBe(200);
        });

        var fromWallet = result.ReadAsJson<Wallet>();
        fromWallet.ShouldNotBeNull();
        fromWallet!.Balance.ShouldBe(700m);

        // Verify destination wallet
        var destResult = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/wallets/{wallet2.Id}");
            x.StatusCodeShouldBe(200);
        });

        var toWallet = destResult.ReadAsJson<Wallet>();
        toWallet.ShouldNotBeNull();
        toWallet!.Balance.ShouldBe(300m);
    }

    [Fact]
    public async Task Transfer_funds_with_insufficient_balance_returns_400()
    {
        // Create two users with wallets
        var user1 = (await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("insuf1@example.com", "Insuf User 1", "password123"))
                .ToUrl("/api/users");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<User>()!;

        var user2 = (await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("insuf2@example.com", "Insuf User 2", "password123"))
                .ToUrl("/api/users");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<User>()!;

        await Task.Delay(1000);

        await _host.Scenario(x =>
        {
            x.Put.Json(new CompleteCustomer(user1.Id, "Insuf1", "Insuf User 1", "PL"))
                .ToUrl($"/api/customers/{user1.Id}/complete");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Put.Json(new CompleteCustomer(user2.Id, "Insuf2", "Insuf User 2", "PL"))
                .ToUrl($"/api/customers/{user2.Id}/complete");
            x.StatusCodeShouldBe(200);
        });

        await Task.Delay(1000);

        var wallets1 = (await _host.Scenario(x =>
        {
            x.Get.Url($"/api/wallets/owner/{user1.Id}");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<List<Wallet>>()!;

        var wallets2 = (await _host.Scenario(x =>
        {
            x.Get.Url($"/api/wallets/owner/{user2.Id}");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<List<Wallet>>()!;

        // wallet1 has 0 balance — try to transfer
        await _host.Scenario(x =>
        {
            x.Post.Json(new TransferFunds(wallets1[0].Id, wallets2[0].Id, 100m))
                .ToUrl("/api/wallets/transfer");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Can_get_wallets()
    {
        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/api/wallets");
            x.StatusCodeShouldBe(200);
        });

        var wallets = result.ReadAsJson<List<Wallet>>();
        wallets.ShouldNotBeNull();
    }

    #endregion

    #region Payments / Deposits

    [Fact]
    public async Task Can_create_deposit()
    {
        var customerId = Guid.NewGuid();

        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateDeposit(customerId, "PLN", 250m))
                .ToUrl("/api/payments/deposits");
            x.StatusCodeShouldBe(200);
        });

        var deposit = result.ReadAsJson<Deposit>();
        deposit.ShouldNotBeNull();
        deposit!.CustomerId.ShouldBe(customerId);
        deposit.Currency.ShouldBe("PLN");
        deposit.Amount.ShouldBe(250m);
        deposit.Status.ShouldBe(DepositStatus.Completed);
    }

    [Fact]
    public async Task Create_deposit_validates_input()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateDeposit(Guid.Empty, "", 0m))
                .ToUrl("/api/payments/deposits");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion
}
