using Administration;
using Alba;
using Marten;
using Meetings;
using MeetingGroupMonolith;
using Microsoft.Extensions.DependencyInjection;
using Payments;
using Registrations;
using Shouldly;
using UserAccess;

namespace Tests;

public class MeetingGroupTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();

        // This is a short hand way to delete all existing
        // data in a Marten document store in one call
        await _host.CleanAllMartenDataAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    #region Registrations

    [Fact]
    public async Task Can_register_user()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser(
                "jdoe",
                "jdoe@example.com",
                "John",
                "Doe",
                "password123"
            )).ToUrl("/api/registrations");

            x.StatusCodeShouldBe(200);
        });

        var user = result.ReadAsJson<User>();
        user.ShouldNotBeNull();
        user!.Id.ShouldNotBe(Guid.Empty);
        user.Login.ShouldBe("jdoe");
        user.Email.ShouldBe("jdoe@example.com");
    }

    [Fact]
    public async Task Register_user_validates_input()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("", "", "", "", "short")).ToUrl("/api/registrations");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Administration

    [Fact]
    public async Task Can_propose_meeting_group()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new ProposeNewMeetingGroup(
                "C# Enthusiasts",
                "A group for C# developers",
                "Portland",
                "US",
                Guid.NewGuid()
            )).ToUrl("/api/administration/proposals");

            x.StatusCodeShouldBe(200);
        });

        var proposal = result.ReadAsJson<MeetingGroupProposal>();
        proposal.ShouldNotBeNull();
        proposal!.Name.ShouldBe("C# Enthusiasts");
        proposal.Status.ShouldBe(ProposalStatus.InVerification);
    }

    [Fact]
    public async Task Can_accept_proposal()
    {
        var userId = Guid.NewGuid();

        // Create proposal
        var created = (await _host.Scenario(x =>
        {
            x.Post.Json(new ProposeNewMeetingGroup(
                "Rust Fans",
                "For Rust lovers",
                "Seattle",
                "US",
                userId
            )).ToUrl("/api/administration/proposals");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<MeetingGroupProposal>()!;

        // Accept it
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new { }).ToUrl($"/api/administration/proposals/{created.Id}/accept");
            x.StatusCodeShouldBe(200);
        });

        var accepted = result.ReadAsJson<MeetingGroupProposal>();
        accepted.ShouldNotBeNull();
        accepted!.Status.ShouldBe(ProposalStatus.Accepted);
        accepted.DecisionDate.ShouldNotBeNull();
    }

    #endregion

    #region Meetings

    [Fact]
    public async Task Can_create_meeting()
    {
        // Set up: create a MeetingGroup via the handler path (propose + accept)
        var userId = Guid.NewGuid();

        var proposal = (await _host.Scenario(x =>
        {
            x.Post.Json(new ProposeNewMeetingGroup("Dev Group", "Devs", "Austin", "US", userId))
                .ToUrl("/api/administration/proposals");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<MeetingGroupProposal>()!;

        await _host.Scenario(x =>
        {
            x.Post.Json(new { }).ToUrl($"/api/administration/proposals/{proposal.Id}/accept");
            x.StatusCodeShouldBe(200);
        });

        // Wait for the cascading handler to create the MeetingGroup
        await Task.Delay(1000);

        // Get the meeting groups to find the one that was created
        var groupsResult = await _host.Scenario(x =>
        {
            x.Get.Url("/api/meeting-groups");
            x.StatusCodeShouldBe(200);
        });

        var groups = groupsResult.ReadAsJson<List<MeetingGroup>>();
        groups.ShouldNotBeNull();
        groups!.ShouldNotBeEmpty();

        var group = groups.First(g => g.Name == "Dev Group");

        // Create meeting
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateMeeting(
                group.Id,
                "Monthly Standup",
                "Our monthly sync",
                DateTime.UtcNow.AddDays(7),
                DateTime.UtcNow.AddDays(7).AddHours(2),
                "123 Conference Rd",
                20,
                0m
            )).ToUrl("/api/meetings");

            x.StatusCodeShouldBe(200);
        });

        var meeting = result.ReadAsJson<Meeting>();
        meeting.ShouldNotBeNull();
        meeting!.Title.ShouldBe("Monthly Standup");
        meeting.MeetingGroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task Can_add_attendee_to_meeting()
    {
        // Set up group and meeting
        var userId = Guid.NewGuid();

        var proposal = (await _host.Scenario(x =>
        {
            x.Post.Json(new ProposeNewMeetingGroup("Attendee Group", "Test", "Denver", "US", userId))
                .ToUrl("/api/administration/proposals");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<MeetingGroupProposal>()!;

        await _host.Scenario(x =>
        {
            x.Post.Json(new { }).ToUrl($"/api/administration/proposals/{proposal.Id}/accept");
            x.StatusCodeShouldBe(200);
        });

        await Task.Delay(1000);

        var groups = (await _host.Scenario(x =>
        {
            x.Get.Url("/api/meeting-groups");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<List<MeetingGroup>>()!;

        var group = groups.First(g => g.Name == "Attendee Group");

        var meeting = (await _host.Scenario(x =>
        {
            x.Post.Json(new CreateMeeting(
                group.Id,
                "Test Meeting",
                "For attendee test",
                DateTime.UtcNow.AddDays(3),
                DateTime.UtcNow.AddDays(3).AddHours(1),
                "456 Test Blvd",
                10,
                5m
            )).ToUrl("/api/meetings");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<Meeting>()!;

        // Add attendee
        var memberId = Guid.NewGuid();
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new AddAttendee(meeting.Id, memberId))
                .ToUrl($"/api/meetings/{meeting.Id}/attendees");
            x.StatusCodeShouldBe(200);
        });

        var updated = result.ReadAsJson<Meeting>();
        updated.ShouldNotBeNull();
        updated!.Attendees.ShouldHaveSingleItem();
        updated.Attendees[0].MemberId.ShouldBe(memberId);
    }

    [Fact]
    public async Task Can_get_meeting_groups()
    {
        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/api/meeting-groups");
            x.StatusCodeShouldBe(200);
        });

        var groups = result.ReadAsJson<List<MeetingGroup>>();
        groups.ShouldNotBeNull();
    }

    [Fact]
    public async Task Can_get_meetings()
    {
        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/api/meetings");
            x.StatusCodeShouldBe(200);
        });

        var meetings = result.ReadAsJson<List<Meeting>>();
        meetings.ShouldNotBeNull();
    }

    #endregion

    #region Payments

    [Fact]
    public async Task Can_create_subscription()
    {
        var payerId = Guid.NewGuid();

        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateSubscription(payerId, "Monthly"))
                .ToUrl("/api/payments/subscriptions");

            x.StatusCodeShouldBe(200);
        });

        var subscriptionId = result.ReadAsJson<Guid>();
        subscriptionId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Create_subscription_validates_period()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateSubscription(Guid.NewGuid(), "Invalid"))
                .ToUrl("/api/payments/subscriptions");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion
}
