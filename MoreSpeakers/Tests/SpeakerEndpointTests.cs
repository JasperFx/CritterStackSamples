using Alba;
using Marten;
using Shouldly;
using Speakers;

namespace MoreSpeakers.Tests;

public class SpeakerEndpointTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();

        await _host.DocumentStore().Advanced.Clean.DeleteAllDocumentsAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Can_register_a_speaker()
    {
        var result = await _host.Scenario(s =>
        {
            s.Post.Json(new RegisterSpeaker("alice@test.com", "Alice", "Test", SpeakerType.New)).ToUrl("/api/speakers");
            s.StatusCodeShouldBeOk();
        });

        var speaker = result.ReadAsJson<Speaker>();
        speaker.ShouldNotBeNull();
        speaker!.FirstName.ShouldBe("Alice");
        speaker.Email.ShouldBe("alice@test.com");
    }

    [Fact]
    public async Task Cannot_register_duplicate_email()
    {
        await _host.Scenario(s =>
        {
            s.Post.Json(new RegisterSpeaker("dup@test.com", "First", "Speaker", SpeakerType.New)).ToUrl("/api/speakers");
            s.StatusCodeShouldBeOk();
        });

        await _host.Scenario(s =>
        {
            s.Post.Json(new RegisterSpeaker("dup@test.com", "Second", "Speaker", SpeakerType.New)).ToUrl("/api/speakers");
            s.StatusCodeShouldBe(409);
        });
    }

    [Fact]
    public async Task Can_get_all_speakers()
    {
        await using var session = _host.DocumentStore().LightweightSession();
        session.Store(new Speaker { FirstName = "Bob", LastName = "Test", Email = "bob@test.com" });
        await session.SaveChangesAsync();

        var result = await _host.Scenario(s =>
        {
            s.Get.Url("/api/speakers");
            s.StatusCodeShouldBeOk();
        });

        var speakers = result.ReadAsJson<Speaker[]>();
        speakers.ShouldNotBeNull();
        (speakers!.Length >= 1).ShouldBeTrue();
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_missing()
    {
        await _host.Scenario(s =>
        {
            s.Get.Url($"/api/speakers/{Guid.NewGuid()}");
            s.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Can_update_speaker_profile()
    {
        await using var session = _host.DocumentStore().LightweightSession();
        var speaker = new Speaker { FirstName = "Original", LastName = "Name", Email = "orig@test.com" };
        session.Store(speaker);
        await session.SaveChangesAsync();

        var result = await _host.Scenario(s =>
        {
            s.Put.Json(new UpdateSpeakerProfile(
                speaker.Id, "Updated", "Name", "New bio", null, null, null,
                true, 3, "Public speaking", ["C#", ".NET"], null
            )).ToUrl($"/api/speakers/{speaker.Id}");
            s.StatusCodeShouldBeOk();
        });

        var updated = result.ReadAsJson<Speaker>();
        updated!.FirstName.ShouldBe("Updated");
        updated.IsAvailableForMentoring.ShouldBeTrue();
        updated.MaxMentees.ShouldBe(3);
        updated.Expertise.ShouldContain("C#");
    }
}
