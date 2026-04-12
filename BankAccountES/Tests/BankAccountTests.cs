using Alba;
using BankAccountES;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace BankAccountES.Tests;

public class BankAccountTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        await _host.CleanAllMartenDataAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<Client> EnrollTestClient(string name = "Jane Doe", string email = "jane@test.com")
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new EnrollClient(name, email)).ToUrl("/api/clients");
            x.StatusCodeShouldBe(200);
        });
        return result.ReadAsJson<Client>()!;
    }

    private async Task<Account> OpenTestAccount(Guid clientId, string currency = "USD")
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new OpenAccount(clientId, currency)).ToUrl("/api/accounts");
            x.StatusCodeShouldBe(200);
        });
        return result.ReadAsJson<Account>()!;
    }

    // --- Client ---

    [Fact]
    public async Task enroll_client()
    {
        var client = await EnrollTestClient("Alice Smith", "alice@test.com");

        client.Id.ShouldNotBe(Guid.Empty);
        client.Name.ShouldBe("Alice Smith");
        client.Email.ShouldBe("alice@test.com");
    }

    [Fact]
    public async Task update_client()
    {
        var client = await EnrollTestClient();

        await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateClient(client.Id, "Updated Name", "updated@test.com"))
                .ToUrl($"/api/clients/{client.Id}");
            x.StatusCodeShouldBe(204);
        });

        var getResult = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/clients/{client.Id}");
            x.StatusCodeShouldBe(200);
        });

        var updated = getResult.ReadAsJson<Client>();
        updated.ShouldNotBeNull();
        updated!.Name.ShouldBe("Updated Name");
        updated.Email.ShouldBe("updated@test.com");
    }

    // --- Account ---

    [Fact]
    public async Task open_account()
    {
        var client = await EnrollTestClient();
        var account = await OpenTestAccount(client.Id);

        account.Id.ShouldNotBe(Guid.Empty);
        account.ClientId.ShouldBe(client.Id);
        account.Currency.ShouldBe("USD");
        account.Balance.ShouldBe(0m);
    }

    [Fact]
    public async Task open_account_with_invalid_client_returns_400()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new OpenAccount(Guid.NewGuid(), "USD")).ToUrl("/api/accounts");
            x.StatusCodeShouldBe(400);
        });
    }

    // --- Deposits ---

    [Fact]
    public async Task deposit_funds()
    {
        var client = await EnrollTestClient();
        var account = await OpenTestAccount(client.Id);

        await _host.Scenario(x =>
        {
            x.Post.Json(new DepositFunds(account.Id, 500m)).ToUrl($"/api/accounts/{account.Id}/deposits");
            x.StatusCodeShouldBe(204);
        });

        var getResult = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/accounts/{account.Id}");
        });

        var updated = getResult.ReadAsJson<Account>();
        updated.ShouldNotBeNull();
        updated!.Balance.ShouldBe(500m);
    }

    // --- Withdrawals ---

    [Fact]
    public async Task withdraw_funds()
    {
        var client = await EnrollTestClient();
        var account = await OpenTestAccount(client.Id);

        await _host.Scenario(x =>
        {
            x.Post.Json(new DepositFunds(account.Id, 1000m)).ToUrl($"/api/accounts/{account.Id}/deposits");
            x.StatusCodeShouldBe(204);
        });

        await _host.Scenario(x =>
        {
            x.Post.Json(new WithdrawFunds(account.Id, 300m)).ToUrl($"/api/accounts/{account.Id}/withdrawals");
            x.StatusCodeShouldBe(204);
        });

        var getResult = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/accounts/{account.Id}");
        });

        var updated = getResult.ReadAsJson<Account>();
        updated.ShouldNotBeNull();
        updated!.Balance.ShouldBe(700m);
    }

    [Fact]
    public async Task withdraw_insufficient_funds_returns_400()
    {
        var client = await EnrollTestClient();
        var account = await OpenTestAccount(client.Id);

        await _host.Scenario(x =>
        {
            x.Post.Json(new DepositFunds(account.Id, 100m)).ToUrl($"/api/accounts/{account.Id}/deposits");
            x.StatusCodeShouldBe(204);
        });

        await _host.Scenario(x =>
        {
            x.Post.Json(new WithdrawFunds(account.Id, 500m)).ToUrl($"/api/accounts/{account.Id}/withdrawals");
            x.StatusCodeShouldBe(400);
        });
    }

    // --- Transaction History ---

    [Fact]
    public async Task get_transactions()
    {
        var client = await EnrollTestClient();
        var account = await OpenTestAccount(client.Id);

        await _host.Scenario(x =>
        {
            x.Post.Json(new DepositFunds(account.Id, 1000m)).ToUrl($"/api/accounts/{account.Id}/deposits");
            x.StatusCodeShouldBe(204);
        });
        await _host.Scenario(x =>
        {
            x.Post.Json(new WithdrawFunds(account.Id, 200m)).ToUrl($"/api/accounts/{account.Id}/withdrawals");
            x.StatusCodeShouldBe(204);
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/accounts/{account.Id}/transactions");
            x.StatusCodeShouldBe(200);
        });

        var txns = result.ReadAsJson<AccountTransactions>();
        txns.ShouldNotBeNull();
        txns!.Transactions.Count.ShouldBe(2);
        txns.Transactions[0].Type.ShouldBe("Deposit");
        txns.Transactions[0].Amount.ShouldBe(1000m);
        txns.Transactions[1].Type.ShouldBe("Withdrawal");
        txns.Transactions[1].Amount.ShouldBe(200m);
        txns.Balance.ShouldBe(800m);
    }

    // --- Query Endpoints ---

    [Fact]
    public async Task get_client()
    {
        var client = await EnrollTestClient("Bob", "bob@test.com");

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/clients/{client.Id}");
            x.StatusCodeShouldBe(200);
        });

        var fetched = result.ReadAsJson<Client>();
        fetched.ShouldNotBeNull();
        fetched!.Name.ShouldBe("Bob");
    }

    [Fact]
    public async Task get_client_accounts()
    {
        var client = await EnrollTestClient();
        await OpenTestAccount(client.Id, "USD");
        await OpenTestAccount(client.Id, "EUR");

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/clients/{client.Id}/accounts");
            x.StatusCodeShouldBe(200);
        });

        var accounts = result.ReadAsJson<List<Account>>();
        accounts.ShouldNotBeNull();
        accounts!.Count.ShouldBe(2);
    }
}
