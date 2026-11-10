using Bobcat;
using Bobcat.Xunit;
using Mentorships;
using Speakers;

namespace MoreSpeakers.Tests;

/// <summary>
/// The original MentorshipEndpointTests, all five rewritten as Bobcat specifications. Each one
/// arranges the stored Speaker and Mentorship documents directly, sends ONE request, and asserts
/// the response and the Mentorship document it left behind. The speaker tests are still plain
/// xUnit v3 tests in <see cref="SpeakerEndpointTests" />.
/// </summary>
[BobcatFeature("Mentorships")]
public class MentorshipSpecs(MoreSpeakersHost app) : MoreSpeakersSpec(app)
{
    private readonly Speaker _mentor = new()
    {
        Id = Guid.NewGuid(), FirstName = "Mentor", LastName = "Speaker", Email = "mentor@test.com",
        Type = SpeakerType.Experienced, IsAvailableForMentoring = true, MaxMentees = 5,
    };

    private readonly Speaker _mentee = new()
    {
        Id = Guid.NewGuid(), FirstName = "Mentee", LastName = "Speaker", Email = "mentee@test.com",
        Type = SpeakerType.New,
    };

    [BobcatSpec(typeof(RequestMentorship))]
    public async Task request_a_mentorship()
    {
        await GivenReadModel(_mentor);
        await GivenReadModel(_mentee);

        await WhenPosted(new RequestMentorship(_mentor.Id, _mentee.Id, MentorshipType.NewToExperienced,
            "Help me with public speaking", ["Public Speaking"], "Weekly"), "/api/mentorships");

        ThenTheResponseStatusIs(200);
        var requested = await TheResponse<Mentorship>();
        await ThenDocument<Mentorship>(requested.Id, Specify<Mentorship>()
            .With(x => x.Status, MentorshipStatus.Pending)
            .With(x => x.MentorId, _mentor.Id)
            .With(x => x.MenteeName, "Mentee Speaker"));
    }

    [BobcatSpec(typeof(RequestMentorship))]
    public async Task nobody_can_mentor_themselves()
    {
        await GivenReadModel(_mentor);

        await WhenPosted(new RequestMentorship(_mentor.Id, _mentor.Id, MentorshipType.ExperiencedToExperienced,
            null, null, null), "/api/mentorships");

        await ThenTheRequestFailsValidationWith(400, "Cannot mentor yourself");
        await ThenNoReadModel<Mentorship>();
    }

    [BobcatSpec(typeof(AcceptMentorship))]
    public async Task accept_a_pending_mentorship()
    {
        var mentorship = new Mentorship
        {
            Id = Guid.NewGuid(),
            MentorId = _mentor.Id, MentorName = _mentor.FullName,
            MenteeId = _mentee.Id, MenteeName = _mentee.FullName,
            Status = MentorshipStatus.Pending, RequestedAt = DateTimeOffset.UtcNow,
        };
        await GivenReadModel(mentorship);

        await WhenPosted(new AcceptMentorship(mentorship.Id, "Happy to help!"), $"/api/mentorships/{mentorship.Id}/accept");

        ThenTheResponseStatusIs(200);
        await ThenDocument<Mentorship>(mentorship.Id, Specify<Mentorship>()
            .With(x => x.Status, MentorshipStatus.Active)
            .With(x => x.ResponseMessage, "Happy to help!"));
    }

    [BobcatSpec(typeof(AcceptMentorship))]
    public async Task a_mentorship_that_is_not_pending_cannot_be_accepted()
    {
        var mentorship = new Mentorship { Id = Guid.NewGuid(), Status = MentorshipStatus.Active, RequestedAt = DateTimeOffset.UtcNow };
        await GivenReadModel(mentorship);

        await WhenPosted(new AcceptMentorship(mentorship.Id, null), $"/api/mentorships/{mentorship.Id}/accept");

        await ThenTheRequestIsRefusedWith(400, "Cannot accept a mentorship in 'Active' status");
        await ThenDocument<Mentorship>(mentorship.Id, Specify<Mentorship>().With(x => x.Status, MentorshipStatus.Active));
    }

    [BobcatSpec(typeof(CompleteMentorship))]
    public async Task complete_an_active_mentorship()
    {
        var mentorship = new Mentorship
        {
            Id = Guid.NewGuid(), Status = MentorshipStatus.Active,
            RequestedAt = DateTimeOffset.UtcNow, StartedAt = DateTimeOffset.UtcNow,
        };
        await GivenReadModel(mentorship);

        await WhenPosted(new CompleteMentorship(mentorship.Id), $"/api/mentorships/{mentorship.Id}/complete");

        ThenTheResponseStatusIs(200);
        var completed = await ThenDocument<Mentorship>(mentorship.Id,
            Specify<Mentorship>().With(x => x.Status, MentorshipStatus.Completed));
        TheCompletionIsStamped(completed);
    }

    [Then("the completion is time-stamped")]
    public void TheCompletionIsStamped(Mentorship mentorship)
        => SpecAssert.Fact(mentorship.CompletedAt is not null, "CompletedAt is set");
}
