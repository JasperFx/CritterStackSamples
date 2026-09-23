using Bobcat;
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
public class ProposalSpecs
{
    [Fact]
    public void An_accepted_home_check_assignment_proposes_a_visit()
    {
        // When HomeCheckAssignmentAccepted is received (assignmentId = {streamId}, ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, volunteerOwnerId = 0e5e0099-0000-0000-0000-000000000099, proposedFor = 2026-10-01T15:00:00Z)
        // Then HomeCheckAppointmentProposed is emitted (ownerId = 0e5e0001-0000-0000-0000-000000000001, kind = HomeCheck, sourceId = {streamId}, scheduledFor = 2026-10-01T15:00:00Z)

        throw new NotImplementedException("ProposeHomeCheckAppointment: An accepted home check assignment proposes a visit");
    }
}
