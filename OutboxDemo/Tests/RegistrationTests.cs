using Alba;
using Marten;
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
        // [EmptyResponse] → 204, no response body
        await _host.Scenario(x =>
        {
            x.Post.Json(new SubmitRegistration("event-1", "member-1", 100m)).ToUrl("/registration");
            x.StatusCodeShouldBe(204);
        });

        // Verify persisted via Marten
        using var session = _host.DocumentStore().LightweightSession();
        var registrations = await session.Query<Registration>()
            .Where(r => r.MemberId == "member-1" && r.EventId == "event-1")
            .ToListAsync();
        Assert.Single(registrations);
        Assert.Equal(100m, registrations[0].Payment);
    }

    [Fact]
    public async Task duplicate_registration_returns_409()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new SubmitRegistration("event-2", "member-2", 50m)).ToUrl("/registration");
            x.StatusCodeShouldBe(204);
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
        await _host.Scenario(x =>
        {
            x.Post.Json(new SubmitRegistration("event-3", "member-3", 200m)).ToUrl("/registration");
            x.StatusCodeShouldBe(204);
        });

        using var session = _host.DocumentStore().LightweightSession();
        var loaded = await session.Query<Registration>()
            .FirstOrDefaultAsync(r => r.MemberId == "member-3" && r.EventId == "event-3");

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
            x.StatusCodeShouldBe(204);
        });

        await _host.Scenario(x =>
        {
            x.Post.Json(new SubmitRegistration("event-5", "member-4", 75m)).ToUrl("/registration");
            x.StatusCodeShouldBe(204);
        });
    }
}
