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

// The original tests, on xUnit v3. Five of them now live in Bobcat specifications: the event-sourced
// Payments module's two in PaymentSpecs, and the three about one module messaging another in
// ModuleMessagingSpecs. These are the plain document CRUD and GET tests, left as they were.
public class MeetingGroupTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();

        // This is a short hand way to delete all existing
        // data in a Marten document store in one call
        await _host.CleanAllMartenDataAsync();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    #region Registrations

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

}
