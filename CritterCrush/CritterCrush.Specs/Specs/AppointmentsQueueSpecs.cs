using Bobcat;
using CritterCrush.Scheduling;
using CritterCrush.Volunteering;
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
[BobcatSlice(SliceType = typeof(AppointmentsQueue))]
[Collection(CritterCrushHost.CollectionName)]
public class AppointmentsQueueSpecs(CritterCrushHost host) : CritterCrushSpec(host)
{
    [Fact]
    /// <summary>
    /// The behaviour that makes this projection multi-stream: two DIFFERENT appointment streams
    /// fold into one shelter's document. A single-stream projection could not express it, so
    /// arranging both streams is the whole point of the scenario.
    /// </summary>
    public async Task The_queue_counts_appointments_from_every_stream_in_the_shelter()
    {
        var shelterId = Guid.NewGuid();
        var confirmed = Guid.NewGuid();
        var awaiting = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEventsOn<Appointment>(confirmed,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, confirmed, at),
            new AppointmentConfirmed(ownerId, shelterId, at));

        await GivenEventsOn<Appointment>(awaiting,
            new SurrenderIntakeAppointmentProposed(Guid.NewGuid(), shelterId, AppointmentKind.SurrenderIntake, awaiting,
                new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero)));

        var queue = await ThenReadModel<AppointmentsQueue>(shelterId);
        // The view carries the key it is folded by — never assigned before the review.
        Assert.Equal(shelterId, queue.ShelterId);
        Assert.Equal(1, queue.AwaitingConfirmation);
        Assert.Equal(1, queue.Confirmed);
        Assert.Equal(0, queue.Closed);
    }

    [Fact]
    public async Task An_appointment_cancelled_before_anyone_confirmed_it_leaves_the_awaiting_count()
    {
        var shelterId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEventsOn<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, at),
            // wasConfirmed: false — cancelled while still awaiting, so it leaves the awaiting count
            // rather than the confirmed one. That flag is why the event carries it at all.
            new AppointmentCancelled(ownerId, shelterId, false, "The volunteer withdrew"));

        var queue = await ThenReadModel<AppointmentsQueue>(shelterId);
        Assert.Equal(0, queue.AwaitingConfirmation);
        Assert.Equal(0, queue.Confirmed);
        Assert.Equal(1, queue.Closed);
    }

    [Fact]
    public async Task A_completed_appointment_leaves_the_queue()
    {
        var shelterId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEventsOn<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, at),
            new AppointmentConfirmed(ownerId, shelterId, at),
            new AppointmentCompleted(ownerId, shelterId, at.AddHours(1)));

        var queue = await ThenReadModel<AppointmentsQueue>(shelterId);
        Assert.Equal(0, queue.AwaitingConfirmation);
        Assert.Equal(0, queue.Confirmed);
        Assert.Equal(1, queue.Closed);
    }
}
