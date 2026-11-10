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

// The original tests, on xUnit v3. Five of them (the cross-module flows that needed Task.Delay) now live
// in PaymentsSpecs as Bobcat specifications; these stay plain: validation, duplicate-email and simple reads.
public class PaymentsTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    #region Users

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

    #endregion

    #region Wallets

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
