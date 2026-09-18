using Bobcat;
using Xunit;
using CritterCrush.Scheduling;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for AppointmentsQueue.
/// </summary>
/// <remarks>
/// Steps render from the marker comments in each test body; the verdict comes from the
/// runner. [BobcatSlice] carries the BINDING only — the slice's domain, chapter and pattern
/// are stated once, on the event model, and merge in by slice name.
/// </remarks>
[BobcatFeature("AppointmentsQueue")]
// TODO — these are integration slices: give this class the store. Derive from (or
// inject) this repository's host/store fixture; the arrange/act/assert helpers are in
// Bobcat.CritterStack. A unit-tested slice needs none of that — see the model's
// spec-ownership manifest for which slices are which.
[BobcatSlice(SliceType = typeof(AppointmentsQueue))]
public class AppointmentsQueueSpecs
{
    [Fact]
    public void The_queue_counts_appointments_from_every_stream_in_the_shelter()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Given SurrenderIntakeAppointmentProposed (ownerId = 0e5e0003-0000-0000-0000-000000000003, shelterId = 5e110001-0000-0000-0000-000000000001, kind = SurrenderIntake, scheduledFor = 2026-10-03T09:00:00Z)
        // Then the AppointmentsQueue read model contains (AwaitingConfirmation = 1, Confirmed = 1, Closed = 0)

        throw new NotImplementedException("AppointmentsQueue: The queue counts appointments from every stream in the shelter");
    }

    [Fact]
    public void An_appointment_cancelled_before_anyone_confirmed_it_leaves_the_awaiting_count()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110003-0000-0000-0000-000000000003, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentCancelled (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110003-0000-0000-0000-000000000003, wasConfirmed = false, reason = The volunteer withdrew)
        // Then the AppointmentsQueue read model contains (AwaitingConfirmation = 0, Confirmed = 0, Closed = 1)

        throw new NotImplementedException("AppointmentsQueue: An appointment cancelled before anyone confirmed it leaves the awaiting count");
    }

    [Fact]
    public void A_completed_appointment_leaves_the_queue()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110002-0000-0000-0000-000000000002, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110002-0000-0000-0000-000000000002)
        // Given AppointmentCompleted (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110002-0000-0000-0000-000000000002, completedAt = 2026-10-01T16:00:00Z)
        // Then the AppointmentsQueue read model contains (AwaitingConfirmation = 0, Confirmed = 0, Closed = 1)

        throw new NotImplementedException("AppointmentsQueue: A completed appointment leaves the queue");
    }
}
