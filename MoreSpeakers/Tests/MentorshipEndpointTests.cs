using Alba;
using Marten;
using Mentorships;
using Shouldly;
using Speakers;

namespace MoreSpeakers.Tests;

public class MentorshipEndpointTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();

        await _host.DocumentStore().Advanced.Clean.DeleteAllDocumentsAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<(Speaker mentor, Speaker mentee)> SeedMentorAndMentee()
    {
        await using var session = _host.DocumentStore().LightweightSession();
        var mentor = new Speaker
        {
            FirstName = "Mentor", LastName = "Speaker", Email = "mentor@test.com",
            Type = SpeakerType.Experienced, IsAvailableForMentoring = true, MaxMentees = 5,
        };
        var mentee = new Speaker
        {
            FirstName = "Mentee", LastName = "Speaker", Email = "mentee@test.com",
            Type = SpeakerType.New,
        };
        session.Store(mentor, mentee);
        await session.SaveChangesAsync();
        return (mentor, mentee);
    }

    [Fact]
    public async Task Can_request_mentorship()
    {
        var (mentor, mentee) = await SeedMentorAndMentee();

        var result = await _host.Scenario(s =>
        {
            s.Post.Json(new RequestMentorship(
                mentor.Id, mentee.Id, MentorshipType.NewToExperienced,
                "Help me with public speaking", ["Public Speaking"], "Weekly"
            )).ToUrl("/api/mentorships");
            s.StatusCodeShouldBeOk();
        });

        var mentorship = result.ReadAsJson<Mentorship>();
        mentorship.ShouldNotBeNull();
        mentorship!.Status.ShouldBe(MentorshipStatus.Pending);
        mentorship.MentorId.ShouldBe(mentor.Id);
    }

    [Fact]
    public async Task Cannot_mentor_yourself()
    {
        var (mentor, _) = await SeedMentorAndMentee();

        await _host.Scenario(s =>
        {
            s.Post.Json(new RequestMentorship(
                mentor.Id, mentor.Id, MentorshipType.ExperiencedToExperienced,
                null, null, null
            )).ToUrl("/api/mentorships");
            s.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Can_accept_mentorship()
    {
        var (mentor, mentee) = await SeedMentorAndMentee();

        await using var session = _host.DocumentStore().LightweightSession();
        var mentorship = new Mentorship
        {
            MentorId = mentor.Id, MentorName = mentor.FullName,
            MenteeId = mentee.Id, MenteeName = mentee.FullName,
            Status = MentorshipStatus.Pending, RequestedAt = DateTimeOffset.UtcNow,
        };
        session.Store(mentorship);
        await session.SaveChangesAsync();

        var result = await _host.Scenario(s =>
        {
            s.Post.Json(new AcceptMentorship(mentorship.Id, "Happy to help!")).ToUrl($"/api/mentorships/{mentorship.Id}/accept");
            s.StatusCodeShouldBeOk();
        });

        var accepted = result.ReadAsJson<Mentorship>();
        accepted!.Status.ShouldBe(MentorshipStatus.Active);
        accepted.ResponseMessage.ShouldBe("Happy to help!");
    }

    [Fact]
    public async Task Cannot_accept_non_pending_mentorship()
    {
        await using var session = _host.DocumentStore().LightweightSession();
        var mentorship = new Mentorship
        {
            Status = MentorshipStatus.Active, RequestedAt = DateTimeOffset.UtcNow,
        };
        session.Store(mentorship);
        await session.SaveChangesAsync();

        await _host.Scenario(s =>
        {
            s.Post.Json(new AcceptMentorship(mentorship.Id, null)).ToUrl($"/api/mentorships/{mentorship.Id}/accept");
            s.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Can_complete_active_mentorship()
    {
        await using var session = _host.DocumentStore().LightweightSession();
        var mentorship = new Mentorship
        {
            Status = MentorshipStatus.Active,
            RequestedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow,
        };
        session.Store(mentorship);
        await session.SaveChangesAsync();

        var result = await _host.Scenario(s =>
        {
            s.Post.Json(new CompleteMentorship(mentorship.Id)).ToUrl($"/api/mentorships/{mentorship.Id}/complete");
            s.StatusCodeShouldBeOk();
        });

        var completed = result.ReadAsJson<Mentorship>();
        completed!.Status.ShouldBe(MentorshipStatus.Completed);
        completed.CompletedAt.ShouldNotBeNull();
    }
}
