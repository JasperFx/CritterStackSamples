using Alba;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using OutboxDemo;

namespace OutboxDemo.Tests;

public class RegistrationTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        await _host.CleanAllMartenDataAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task submit_registration()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new SubmitRegistration("event-1", "member-1", 100m)).ToUrl("/registration");
            x.StatusCodeShouldBe(200);
        });

        var registration = result.ReadAsJson<Registration>();
        Assert.NotNull(registration);
        Assert.NotEqual(Guid.Empty, registration!.Id);
        Assert.Equal("member-1", registration.MemberId);
        Assert.Equal("event-1", registration.EventId);
        Assert.Equal(100m, registration.Payment);
    }

    [Fact]
    public async Task duplicate_registration_returns_409()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new SubmitRegistration("event-2", "member-2", 50m)).ToUrl("/registration");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Post.Json(new SubmitRegistration("event-2", "member-2", 75m)).ToUrl("/registration");
            x.StatusCodeShouldBe(409);
        });
    }

    [Fact]
    public async Task registration_is_persisted()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new SubmitRegistration("event-3", "member-3", 200m)).ToUrl("/registration");
            x.StatusCodeShouldBe(200);
        });

        var registration = result.ReadAsJson<Registration>()!;

        await using var session = _host.Services.GetRequiredService<IDocumentStore>().QuerySession();
        var loaded = await session.LoadAsync<Registration>(registration.Id);

        Assert.NotNull(loaded);
        Assert.Equal("member-3", loaded!.MemberId);
        Assert.Equal("event-3", loaded.EventId);
    }

    [Fact]
    public async Task same_member_different_event_is_allowed()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new SubmitRegistration("event-4", "member-4", 50m)).ToUrl("/registration");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Post.Json(new SubmitRegistration("event-5", "member-4", 75m)).ToUrl("/registration");
            x.StatusCodeShouldBe(200);
        });
    }
}
