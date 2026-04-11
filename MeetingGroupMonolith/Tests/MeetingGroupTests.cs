using Administration;
using Alba;
using Marten;
using Meetings;
using MeetingGroupMonolith;
using Microsoft.Extensions.DependencyInjection;
using Payments;
using Registrations;
using UserAccess;

namespace Tests;

public class MeetingGroupTests : IAsyncLifetime
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
        Assert.NotNull(user);
        Assert.NotEqual(Guid.Empty, user!.Id);
        Assert.Equal("jdoe", user.Login);
        Assert.Equal("jdoe@example.com", user.Email);
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
        Assert.NotNull(proposal);
        Assert.Equal("C# Enthusiasts", proposal!.Name);
        Assert.Equal(ProposalStatus.InVerification, proposal.Status);
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
        Assert.NotNull(accepted);
        Assert.Equal(ProposalStatus.Accepted, accepted!.Status);
        Assert.NotNull(accepted.DecisionDate);
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
        Assert.NotNull(groups);
        Assert.NotEmpty(groups!);

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
        Assert.NotNull(meeting);
        Assert.Equal("Monthly Standup", meeting!.Title);
        Assert.Equal(group.Id, meeting.MeetingGroupId);
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
        Assert.NotNull(updated);
        Assert.Single(updated!.Attendees);
        Assert.Equal(memberId, updated.Attendees[0].MemberId);
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
        Assert.NotNull(groups);
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
        Assert.NotNull(meetings);
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
        Assert.NotEqual(Guid.Empty, subscriptionId);
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
