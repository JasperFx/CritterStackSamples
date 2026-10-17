using Bobcat;
using Bobcat.Xunit;
using CritterCrush.Scheduling;
using CritterCrush.Volunteering;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for MyAppointments.
/// </summary>
/// <remarks>
/// Steps render from the decorated helpers on <see cref="CritterCrushSpec"/>; the verdict comes from
/// the runner. <c>[BobcatSpec]</c> is the <c>[Fact]</c>, opens the recording and carries the slice
/// BINDING only — the slice's domain, chapter and pattern are stated once, on the event model
/// (CritterCrushEventModel.cs in the app), and merge in by slice name. Through Bobcat 0.27 this was
/// <c>[Fact]</c> + <c>[BobcatSlice]</c> with <c>[BobcatScenario]</c> on the class, and forgetting the
/// last one rendered nothing at all (issue #379); <c>[BobcatSpec]</c> cannot come apart that way.
/// </remarks>
[BobcatFeature("MyAppointments")]
[Collection(CritterCrushHost.CollectionName)]
public class MyAppointmentsSpecs(CritterCrushHost host) : CritterCrushSpec(host)
{
    [BobcatSpec(typeof(MyAppointments))]
    public async Task An_owner_sees_an_appointment_awaiting_their_confirmation()
    {
        var ownerId = Guid.NewGuid();
        var id = Guid.NewGuid();

        await GivenEventsOn<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, Guid.NewGuid(), AppointmentKind.HomeCheck, id,
                new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero)));

        var mine = await ThenReadModel<MyAppointments>(ownerId);
        // The view carries the key it is folded by — never assigned before the review.
        Assert.Equal(ownerId, mine.OwnerId);
        Assert.Equal(1, mine.AwaitingConfirmation);
        Assert.Equal(0, mine.Confirmed);
        Assert.Equal(0, mine.Closed);
    }

    /// <summary>
    /// The multi-stream fold from the owner's side: one person, two appointment streams, one page.
    /// </summary>
    [BobcatSpec(typeof(MyAppointments))]
    public async Task An_owner_page_spans_every_appointment_stream_they_have()
    {
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var confirmed = Guid.NewGuid();
        var awaiting = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEventsOn<Appointment>(confirmed,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, confirmed, at),
            new AppointmentConfirmed(ownerId, shelterId, at));

        await GivenEventsOn<Appointment>(awaiting,
            new SurrenderIntakeAppointmentProposed(ownerId, shelterId, AppointmentKind.SurrenderIntake, awaiting,
                new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero)));

        var mine = await ThenReadModel<MyAppointments>(ownerId);
        Assert.Equal(1, mine.AwaitingConfirmation);
        Assert.Equal(1, mine.Confirmed);
        Assert.Equal(0, mine.Closed);
    }
}
