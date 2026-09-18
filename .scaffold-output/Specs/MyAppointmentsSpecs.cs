using Bobcat;
using Xunit;
using CritterCrush.Scheduling;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for MyAppointments.
/// </summary>
/// <remarks>
/// Steps render from the marker comments in each test body; the verdict comes from the
/// runner. [BobcatSlice] carries the BINDING only — the slice's domain, chapter and pattern
/// are stated once, on the event model, and merge in by slice name.
/// </remarks>
[BobcatFeature("MyAppointments")]
[Collection(CritterCrushHost.CollectionName)]
[BobcatSlice(SliceType = typeof(MyAppointments))]
public class MyAppointmentsSpecs(CritterCrushHost fixture) : CritterCrushSpec(fixture)
{
    [Fact]
    public void An_owner_sees_an_appointment_awaiting_their_confirmation()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0004-0000-0000-0000-000000000004, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Then the MyAppointments read model contains (AwaitingConfirmation = 1, Confirmed = 0, Closed = 0)

        throw new NotImplementedException("MyAppointments: An owner sees an appointment awaiting their confirmation");
    }

    [Fact]
    public void An_owner_page_spans_every_appointment_stream_they_have()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0005-0000-0000-0000-000000000005, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0005-0000-0000-0000-000000000005, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Given SurrenderIntakeAppointmentProposed (ownerId = 0e5e0005-0000-0000-0000-000000000005, shelterId = 5e110001-0000-0000-0000-000000000001, kind = SurrenderIntake, scheduledFor = 2026-10-03T09:00:00Z)
        // Then the MyAppointments read model contains (AwaitingConfirmation = 1, Confirmed = 1, Closed = 0)

        throw new NotImplementedException("MyAppointments: An owner page spans every appointment stream they have");
    }
}
