using Bobcat;
using CritterCrush.Scheduling;
using CritterCrush.Volunteering;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for ProposeHomeCheckAppointment.
/// </summary>
/// <remarks>
/// Steps render from the marker comments in each test body; the verdict comes from the
/// runner. [BobcatSlice] carries the BINDING only — the slice's domain, chapter and pattern
/// are stated once, on the event model, and merge in by slice name.
/// </remarks>
[BobcatFeature("BookingAppointments")]
[BobcatSlice(SliceName = "ProposeHomeCheckAppointment")]
[Collection(CritterCrushHost.CollectionName)]
public class ProposalSpecs(CritterCrushHost host) : CritterCrushSpec(host)
{
    /// <summary>
    /// The slice #324 opens with: a trigger event in, one event out, plus the stream it starts.
    /// The decision that matters is that the appointment's stream IS the assignment that asked for
    /// it — at-least-once delivery then collides on StartStream instead of booking a second visit —
    /// and this asserts it directly rather than through the store.
    /// </summary>
    [Fact]
    public async Task An_accepted_home_check_assignment_proposes_a_visit()
    {
        var assignmentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var proposedFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await WhenReceived(new HomeCheckAssignmentAccepted(
            assignmentId, ownerId, shelterId, Guid.NewGuid(), proposedFor));

        ThenEvents(typeof(HomeCheckAppointmentProposed));

        var proposed = TheEvent<HomeCheckAppointmentProposed>();
        Assert.Equal(ownerId, proposed.OwnerId);
        Assert.Equal(AppointmentKind.HomeCheck, proposed.Kind);
        Assert.Equal(proposedFor, proposed.ScheduledFor);

        // The one decision worth specifying: the appointment's stream is the assignment's id.
        Assert.Equal(assignmentId, proposed.SourceId);
    }
}
