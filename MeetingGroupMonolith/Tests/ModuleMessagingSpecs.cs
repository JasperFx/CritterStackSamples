using Administration;
using Bobcat;
using Bobcat.Xunit;
using MeetingGroupMonolith;
using Meetings;
using Payments;
using Registrations;
using UserAccess;

namespace Tests;

/// <summary>
/// The three original tests whose point is one module telling another something over a durable local
/// queue, as Bobcat specifications on <see cref="Wolverine.Bobcat.WolverineSpec" />. Each arranges the
/// documents it needs directly (no propose-then-accept chain, no <c>Task.Delay</c>), sends ONE request,
/// and asserts the integration event that crossed the module boundary and what the receiving module did
/// with it.
/// </summary>
[BobcatFeature("Module messaging")]
public class ModuleMessagingSpecs(MeetingGroupsHost app) : ModuleMessagingSpec(app)
{
    [BobcatSpec(typeof(RegisterUser))]
    public async Task registering_a_user_makes_them_a_member()
    {
        await WhenPosted(new RegisterUser("jdoe", "jdoe@example.com", "John", "Doe", "password123"), "/api/registrations");

        ThenTheResponseStatusIs(200);
        ThenMessageSent(Specify<NewUserRegisteredEvent>().With(x => x.Login, "jdoe").With(x => x.Email, "jdoe@example.com"));
        var user = await TheResponse<User>();
        await ThenDocument<Member>(user.Id, Specify<Member>()
            .With(x => x.Login, "jdoe")
            .With(x => x.Email, "jdoe@example.com")
            .With(x => x.FirstName, "John"));
    }

    [BobcatSpec]
    public async Task accepting_a_proposal_creates_the_meeting_group()
    {
        var proposalId = Guid.NewGuid();
        var proposerId = Guid.NewGuid();
        await GivenReadModel(new MeetingGroupProposal
        {
            Id = proposalId, Name = "Rust Fans", Description = "For Rust lovers",
            LocationCity = "Seattle", LocationCountryCode = "US", ProposalUserId = proposerId,
        });

        await WhenPostedTo($"/api/administration/proposals/{proposalId}/accept");

        ThenTheResponseStatusIs(200);
        var proposal = await ThenDocument<MeetingGroupProposal>(proposalId,
            Specify<MeetingGroupProposal>().With(x => x.Status, ProposalStatus.Accepted));
        ThenTheDecisionIsDated(proposal);
        ThenMessageSent(Specify<MeetingGroupProposalAcceptedEvent>().With(x => x.ProposalId, proposalId).With(x => x.Name, "Rust Fans"));
        await ThenTheMeetingGroupExists("Rust Fans", "Seattle", proposerId);
    }

    [BobcatSpec(typeof(AddAttendee))]
    public async Task adding_an_attendee_opens_a_meeting_fee()
    {
        var meetingId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        await GivenReadModel(new Meeting
        {
            Id = meetingId, MeetingGroupId = groupId, Title = "Test Meeting",
            AttendeesLimit = 10, Fee = 5m,
        });
        var memberId = Guid.NewGuid();

        await WhenPosted(new AddAttendee(meetingId, memberId), $"/api/meetings/{meetingId}/attendees");

        ThenTheResponseStatusIs(200);
        await ThenTheMeetingHasTheAttendee(meetingId, memberId);
        ThenMessageSent(Specify<MeetingAttendeeAddedEvent>()
            .With(x => x.MeetingId, meetingId)
            .With(x => x.AttendeeId, memberId)
            .With(x => x.MeetingGroupId, groupId));
        ThenAMeetingFeeWasOpened(memberId, meetingId);
    }

    [Then("the decision is dated")]
    public void ThenTheDecisionIsDated(MeetingGroupProposal proposal)
        => SpecAssert.Fact(proposal.DecisionDate is not null, "The accepted proposal has no decision date.");

    [Then("the meeting group \"{name}\" in {city} exists, organised by {organizerId}")]
    public async Task ThenTheMeetingGroupExists(string name, string city, Guid organizerId)
    {
        // The Meetings module's handler mints the group's id, so it is found by name
        var groups = (await AllDocuments<MeetingGroup>()).Where(x => x.Name == name).ToList();
        SpecAssert.Check("meeting groups named " + name, groups.Count, 1);
        if (groups.Count != 1) return;

        SpecAssert.Check("city", groups[0].LocationCity, city);
        SpecAssert.Check("creator", groups[0].CreatorId, organizerId);
        SpecAssert.Check("organizer", groups[0].Members.SingleOrDefault(m => m.Role == "Organizer")?.MemberId, organizerId);
    }

    [Then("meeting {meetingId} has the one attendee {memberId}")]
    public async Task ThenTheMeetingHasTheAttendee(Guid meetingId, Guid memberId)
    {
        var meeting = await ThenDocument<Meeting>(meetingId);
        SpecAssert.Check("attendees", string.Join(", ", meeting.Attendees.Select(a => a.MemberId)), memberId.ToString());
    }

    /// <summary>
    /// The Payments module's handler starts the fee stream off the cascaded MeetingAttendeeAddedEvent, so the
    /// event is in <c>LastAct.AllNewEvents</c> (everything the tracked session saw committed), not in the
    /// act's own events that <c>ThenEvents</c> judges.
    /// </summary>
    [Then("the Payments module opened a meeting fee for {memberId} on meeting {meetingId}")]
    public void ThenAMeetingFeeWasOpened(Guid memberId, Guid meetingId)
        => SpecAssert.Fact(
            LastAct.AllNewEvents.Any(e => e.Data is MeetingFeeCreated fee && fee.PayerId == memberId && fee.MeetingId == meetingId),
            $"No MeetingFeeCreated for member {memberId} on meeting {meetingId} among the " +
            $"{LastAct.AllNewEvents.Count} event(s) the act caused: " +
            string.Join(", ", LastAct.AllNewEvents.Select(e => e.Data.GetType().Name)));
}
