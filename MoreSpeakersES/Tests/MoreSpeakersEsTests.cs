using Alba;
using Marten;
using Mentorships;
using Microsoft.Extensions.DependencyInjection;
using Speakers;

namespace Tests;

public class MoreSpeakersEsTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    #region Helpers

    private async Task<Speaker> RegisterSpeaker(string email, string first, string last, SpeakerType type = SpeakerType.Experienced)
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterSpeaker(email, first, last, type)).ToUrl("/api/speakers");
            x.StatusCodeShouldBeOk();
        });
        return result.ReadAsJson<Speaker>()!;
    }

    private async Task<Speaker> RegisterMentor(string email, string first, string last)
    {
        var speaker = await RegisterSpeaker(email, first, last, SpeakerType.Experienced);

        await _host.Scenario(x =>
        {
            x.Put.Json(new ChangeMentoringAvailability(speaker.Id, true, 5, "General")).ToUrl($"/api/speakers/{speaker.Id}/mentoring");
            x.StatusCodeShouldBeOk();
        });

        return speaker;
    }

    private async Task<Mentorship> RequestMentorship(Guid mentorId, Guid menteeId)
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new RequestMentorship(mentorId, menteeId, MentorshipType.NewToExperienced, "Please mentor me", ["C#"], "Weekly")).ToUrl("/api/mentorships");
            x.StatusCodeShouldBeOk();
        });
        return result.ReadAsJson<Mentorship>()!;
    }

    #endregion

    #region Speaker Registration

    [Fact]
    public async Task register_speaker_returns_speaker()
    {
        var speaker = await RegisterSpeaker("alice@test.com", "Alice", "Smith");

        Assert.NotEqual(Guid.Empty, speaker.Id);
        Assert.Equal("alice@test.com", speaker.Email);
        Assert.Equal("Alice", speaker.FirstName);
        Assert.Equal("Smith", speaker.LastName);
        Assert.Equal(SpeakerType.Experienced, speaker.Type);
    }

    [Fact]
    public async Task register_speaker_rejects_duplicate_email()
    {
        await RegisterSpeaker("dup@test.com", "First", "Speaker");

        await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterSpeaker("dup@test.com", "Second", "Speaker", SpeakerType.New)).ToUrl("/api/speakers");
            x.StatusCodeShouldBe(409);
        });
    }

    [Fact]
    public async Task register_speaker_rejects_invalid_email()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterSpeaker("not-an-email", "Bad", "Email", SpeakerType.New)).ToUrl("/api/speakers");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task register_speaker_rejects_empty_name()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterSpeaker("ok@test.com", "", "Last", SpeakerType.New)).ToUrl("/api/speakers");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Update Profile

    [Fact]
    public async Task update_speaker_profile()
    {
        var speaker = await RegisterSpeaker("update@test.com", "Original", "Name");

        var result = await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateSpeakerProfile(
                speaker.Id, "Updated", "Name", "My bio", "My goals", "http://headshot.png",
                ["C#", ".NET"], [new SocialLink { Platform = "Twitter", Url = "https://twitter.com/test" }]
            )).ToUrl($"/api/speakers/{speaker.Id}");
            x.StatusCodeShouldBeOk();
        });

        // Verify via GET
        var getResult = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/speakers/{speaker.Id}");
            x.StatusCodeShouldBeOk();
        });

        var updated = getResult.ReadAsJson<Speaker>()!;
        Assert.Equal("Updated", updated.FirstName);
        Assert.Equal("My bio", updated.Bio);
        Assert.Equal("My goals", updated.Goals);
        Assert.Contains("C#", updated.Expertise);
        Assert.Single(updated.SocialLinks);
    }

    [Fact]
    public async Task update_speaker_profile_rejects_empty_name()
    {
        var speaker = await RegisterSpeaker("valid@test.com", "Valid", "Speaker");

        await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateSpeakerProfile(speaker.Id, "", "Name", null, null, null, [], [])).ToUrl($"/api/speakers/{speaker.Id}");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Mentoring Availability

    [Fact]
    public async Task change_mentoring_availability()
    {
        var speaker = await RegisterSpeaker("mentor@test.com", "Mentor", "Speaker");

        await _host.Scenario(x =>
        {
            x.Put.Json(new ChangeMentoringAvailability(speaker.Id, true, 3, "Architecture")).ToUrl($"/api/speakers/{speaker.Id}/mentoring");
            x.StatusCodeShouldBeOk();
        });

        var getResult = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/speakers/{speaker.Id}");
        });

        var updated = getResult.ReadAsJson<Speaker>()!;
        Assert.True(updated.IsAvailableForMentoring);
        Assert.Equal(3, updated.MaxMentees);
        Assert.Equal("Architecture", updated.MentorshipFocus);
    }

    #endregion

    #region Get Speakers

    [Fact]
    public async Task get_speakers_returns_list()
    {
        await RegisterSpeaker("list1@test.com", "One", "Speaker");
        await RegisterSpeaker("list2@test.com", "Two", "Speaker");

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/api/speakers");
            x.StatusCodeShouldBeOk();
        });

        var speakers = result.ReadAsJson<List<Speaker>>()!;
        Assert.True(speakers.Count >= 2);
    }

    [Fact]
    public async Task get_speaker_by_id_returns_speaker()
    {
        var speaker = await RegisterSpeaker("byid@test.com", "ById", "Test");

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/speakers/{speaker.Id}");
            x.StatusCodeShouldBeOk();
        });

        var fetched = result.ReadAsJson<Speaker>()!;
        Assert.Equal(speaker.Id, fetched.Id);
        Assert.Equal("byid@test.com", fetched.Email);
    }

    [Fact]
    public async Task get_speaker_by_id_returns_404_for_missing()
    {
        await _host.Scenario(x =>
        {
            x.Get.Url($"/api/speakers/{Guid.NewGuid()}");
            x.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task get_available_mentors_returns_only_available_experienced()
    {
        var mentor = await RegisterMentor("avail@test.com", "Available", "Mentor");

        // Register a new speaker who is NOT available for mentoring
        await RegisterSpeaker("notavail@test.com", "Not", "Available", SpeakerType.New);

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/api/speakers/mentors");
            x.StatusCodeShouldBeOk();
        });

        var mentors = result.ReadAsJson<List<Speaker>>()!;
        Assert.Contains(mentors, m => m.Id == mentor.Id);
    }

    #endregion

    #region Request Mentorship

    [Fact]
    public async Task request_mentorship_creates_pending_mentorship()
    {
        var mentor = await RegisterMentor("rm_mentor@test.com", "Req", "Mentor");
        var mentee = await RegisterSpeaker("rm_mentee@test.com", "Req", "Mentee", SpeakerType.New);

        var mentorship = await RequestMentorship(mentor.Id, mentee.Id);

        Assert.NotEqual(Guid.Empty, mentorship.Id);
        Assert.Equal(mentor.Id, mentorship.MentorId);
        Assert.Equal(mentee.Id, mentorship.MenteeId);
        Assert.Equal(MentorshipStatus.Pending, mentorship.Status);
        Assert.Equal(MentorshipType.NewToExperienced, mentorship.Type);
        Assert.Equal("Please mentor me", mentorship.RequestMessage);
    }

    [Fact]
    public async Task cannot_mentor_yourself()
    {
        var speaker = await RegisterMentor("self@test.com", "Self", "Mentor");

        await _host.Scenario(x =>
        {
            x.Post.Json(new RequestMentorship(speaker.Id, speaker.Id, MentorshipType.NewToExperienced, null, null, null)).ToUrl("/api/mentorships");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task cannot_request_mentorship_from_unavailable_mentor()
    {
        // Register speaker but do NOT enable mentoring
        var speaker = await RegisterSpeaker("nomentor@test.com", "Not", "Mentor");
        var mentee = await RegisterSpeaker("wannabe@test.com", "Wanna", "Mentee", SpeakerType.New);

        await _host.Scenario(x =>
        {
            x.Post.Json(new RequestMentorship(speaker.Id, mentee.Id, MentorshipType.NewToExperienced, null, null, null)).ToUrl("/api/mentorships");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Accept Mentorship

    [Fact]
    public async Task accept_mentorship_transitions_to_active()
    {
        var mentor = await RegisterMentor("acc_mentor@test.com", "Acc", "Mentor");
        var mentee = await RegisterSpeaker("acc_mentee@test.com", "Acc", "Mentee", SpeakerType.New);
        var mentorship = await RequestMentorship(mentor.Id, mentee.Id);

        await _host.Scenario(x =>
        {
            x.Post.Json(new AcceptMentorship(mentorship.Id, "Welcome aboard!")).ToUrl($"/api/mentorships/{mentorship.Id}/accept");
            x.StatusCodeShouldBeOk();
        });

        // Verify status
        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/mentorships/{mentorship.Id}");
        });
        var updated = result.ReadAsJson<Mentorship>()!;
        Assert.Equal(MentorshipStatus.Active, updated.Status);
        Assert.Equal("Welcome aboard!", updated.ResponseMessage);
    }

    [Fact]
    public async Task cannot_accept_already_accepted_mentorship()
    {
        var mentor = await RegisterMentor("aa_mentor@test.com", "AA", "Mentor");
        var mentee = await RegisterSpeaker("aa_mentee@test.com", "AA", "Mentee", SpeakerType.New);
        var mentorship = await RequestMentorship(mentor.Id, mentee.Id);

        await _host.Scenario(x =>
        {
            x.Post.Json(new AcceptMentorship(mentorship.Id, null)).ToUrl($"/api/mentorships/{mentorship.Id}/accept");
            x.StatusCodeShouldBeOk();
        });

        // Second accept should fail
        await _host.Scenario(x =>
        {
            x.Post.Json(new AcceptMentorship(mentorship.Id, null)).ToUrl($"/api/mentorships/{mentorship.Id}/accept");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Decline Mentorship

    [Fact]
    public async Task decline_mentorship_transitions_to_declined()
    {
        var mentor = await RegisterMentor("dec_mentor@test.com", "Dec", "Mentor");
        var mentee = await RegisterSpeaker("dec_mentee@test.com", "Dec", "Mentee", SpeakerType.New);
        var mentorship = await RequestMentorship(mentor.Id, mentee.Id);

        await _host.Scenario(x =>
        {
            x.Post.Json(new DeclineMentorship(mentorship.Id, "Too busy right now")).ToUrl($"/api/mentorships/{mentorship.Id}/decline");
            x.StatusCodeShouldBeOk();
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/mentorships/{mentorship.Id}");
        });
        var updated = result.ReadAsJson<Mentorship>()!;
        Assert.Equal(MentorshipStatus.Declined, updated.Status);
        Assert.Equal("Too busy right now", updated.ResponseMessage);
    }

    [Fact]
    public async Task cannot_decline_already_declined_mentorship()
    {
        var mentor = await RegisterMentor("dd_mentor@test.com", "DD", "Mentor");
        var mentee = await RegisterSpeaker("dd_mentee@test.com", "DD", "Mentee", SpeakerType.New);
        var mentorship = await RequestMentorship(mentor.Id, mentee.Id);

        await _host.Scenario(x =>
        {
            x.Post.Json(new DeclineMentorship(mentorship.Id, null)).ToUrl($"/api/mentorships/{mentorship.Id}/decline");
        });

        await _host.Scenario(x =>
        {
            x.Post.Json(new DeclineMentorship(mentorship.Id, null)).ToUrl($"/api/mentorships/{mentorship.Id}/decline");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Complete Mentorship

    [Fact]
    public async Task complete_mentorship_transitions_to_completed()
    {
        var mentor = await RegisterMentor("comp_mentor@test.com", "Comp", "Mentor");
        var mentee = await RegisterSpeaker("comp_mentee@test.com", "Comp", "Mentee", SpeakerType.New);
        var mentorship = await RequestMentorship(mentor.Id, mentee.Id);

        // Accept first
        await _host.Scenario(x =>
        {
            x.Post.Json(new AcceptMentorship(mentorship.Id, null)).ToUrl($"/api/mentorships/{mentorship.Id}/accept");
        });

        // Complete
        await _host.Scenario(x =>
        {
            x.Post.Json(new CompleteMentorship(mentorship.Id)).ToUrl($"/api/mentorships/{mentorship.Id}/complete");
            x.StatusCodeShouldBeOk();
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/mentorships/{mentorship.Id}");
        });
        var updated = result.ReadAsJson<Mentorship>()!;
        Assert.Equal(MentorshipStatus.Completed, updated.Status);
    }

    [Fact]
    public async Task cannot_complete_pending_mentorship()
    {
        var mentor = await RegisterMentor("cp_mentor@test.com", "CP", "Mentor");
        var mentee = await RegisterSpeaker("cp_mentee@test.com", "CP", "Mentee", SpeakerType.New);
        var mentorship = await RequestMentorship(mentor.Id, mentee.Id);

        await _host.Scenario(x =>
        {
            x.Post.Json(new CompleteMentorship(mentorship.Id)).ToUrl($"/api/mentorships/{mentorship.Id}/complete");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Get Mentorships

    [Fact]
    public async Task get_mentorship_by_id()
    {
        var mentor = await RegisterMentor("gm_mentor@test.com", "GM", "Mentor");
        var mentee = await RegisterSpeaker("gm_mentee@test.com", "GM", "Mentee", SpeakerType.New);
        var mentorship = await RequestMentorship(mentor.Id, mentee.Id);

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/mentorships/{mentorship.Id}");
            x.StatusCodeShouldBeOk();
        });

        var fetched = result.ReadAsJson<Mentorship>()!;
        Assert.Equal(mentorship.Id, fetched.Id);
    }

    [Fact]
    public async Task get_mentorships_for_mentor()
    {
        var mentor = await RegisterMentor("fm_mentor@test.com", "FM", "Mentor");
        var mentee = await RegisterSpeaker("fm_mentee@test.com", "FM", "Mentee", SpeakerType.New);
        await RequestMentorship(mentor.Id, mentee.Id);

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/mentorships/mentor/{mentor.Id}");
            x.StatusCodeShouldBeOk();
        });

        var mentorships = result.ReadAsJson<List<Mentorship>>()!;
        Assert.NotEmpty(mentorships);
        Assert.All(mentorships, m => Assert.Equal(mentor.Id, m.MentorId));
    }

    [Fact]
    public async Task get_mentorships_for_mentee()
    {
        var mentor = await RegisterMentor("fe_mentor@test.com", "FE", "Mentor");
        var mentee = await RegisterSpeaker("fe_mentee@test.com", "FE", "Mentee", SpeakerType.New);
        await RequestMentorship(mentor.Id, mentee.Id);

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/mentorships/mentee/{mentee.Id}");
            x.StatusCodeShouldBeOk();
        });

        var mentorships = result.ReadAsJson<List<Mentorship>>()!;
        Assert.NotEmpty(mentorships);
        Assert.All(mentorships, m => Assert.Equal(mentee.Id, m.MenteeId));
    }

    #endregion
}
