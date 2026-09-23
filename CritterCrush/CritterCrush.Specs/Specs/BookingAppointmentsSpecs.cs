using Bobcat;
using Bobcat.Xunit;
using CritterCrush.Scheduling;
using CritterCrush.Volunteering;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for ProposeFosterHandoverAppointment, ProposeSurrenderIntakeAppointment, ConfirmAppointment, RequestReschedule, RescheduleAppointment, CompleteAppointment, CancelAppointment, RecordAppointmentNoShow.
/// </summary>
/// <remarks>
/// Steps render from the marker comments in each test body; the verdict comes from the
/// runner. [BobcatSlice] carries the BINDING only — the slice's domain, chapter and pattern
/// are stated once, on the event model, and merge in by slice name.
/// </remarks>
[BobcatScenario]
[BobcatFeature("BookingAppointments")]
[Collection(CritterCrushHost.CollectionName)]
public class BookingAppointmentsSpecs(CritterCrushHost host) : CritterCrushSpec(host)
{
    [Fact]
    [BobcatSlice(SliceName = "ProposeFosterHandoverAppointment")]
    public async Task A_dog_placed_in_foster_proposes_a_handover()
    {
        var fosterApplicationId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var proposedFor = new DateTimeOffset(2026, 10, 2, 10, 30, 0, TimeSpan.Zero);

        await WhenReceived(new DogPlacedInFoster(fosterApplicationId, ownerId, Guid.NewGuid(), proposedFor));

        ThenEvents(typeof(FosterHandoverAppointmentProposed));

        var proposed = TheEvent<FosterHandoverAppointmentProposed>();
        Assert.Equal(ownerId, proposed.OwnerId);
        Assert.Equal(AppointmentKind.FosterHandover, proposed.Kind);
        Assert.Equal(proposedFor, proposed.ScheduledFor);
        Assert.Equal(fosterApplicationId, proposed.SourceId);
    }

    [Fact]
    [BobcatSlice(SliceName = "ProposeSurrenderIntakeAppointment")]
    public async Task A_reviewed_surrender_request_proposes_an_intake()
    {
        var surrenderRequestId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var proposedFor = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

        await WhenReceived(new SurrenderRequestReviewed(surrenderRequestId, ownerId, Guid.NewGuid(), proposedFor));

        ThenEvents(typeof(SurrenderIntakeAppointmentProposed));

        var proposed = TheEvent<SurrenderIntakeAppointmentProposed>();
        Assert.Equal(ownerId, proposed.OwnerId);
        Assert.Equal(AppointmentKind.SurrenderIntake, proposed.Kind);
        Assert.Equal(surrenderRequestId, proposed.SourceId);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ConfirmAppointment))]
    public async Task A_proposed_appointment_is_confirmed()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor));

        await WhenPosted(new ConfirmAppointment(id), "/api/scheduling/confirmappointment");

        ThenEvents(typeof(AppointmentConfirmed));
        Assert.Equal(ownerId, TheEvent<AppointmentConfirmed>().OwnerId);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ConfirmAppointment))]
    public async Task A_cancelled_appointment_cannot_be_confirmed()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentCancelled(ownerId, shelterId, false, "The volunteer withdrew"));

        await WhenPosted(new ConfirmAppointment(id), "/api/scheduling/confirmappointment");

        // A collapsed endpoint answers 400 rather than throwing, so the MESSAGE is what says which
        // rule refused — every guard here answers the same status.
        await ThenRefusedWith("This appointment was cancelled");
        ThenNoEvents();
    }

    /// <summary>
    /// Added during review. RecordAppointmentNoShow's summary said "reachable only from Confirmed"
    /// and its guard checked only IsClosed, so an appointment nobody had confirmed could be marked
    /// a no-show — leaving the shelter queue at Confirmed=-1, Awaiting=1, Closed=1.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(RecordAppointmentNoShow))]
    public async Task An_appointment_nobody_confirmed_is_not_a_no_show()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor));

        await WhenPosted(new RecordAppointmentNoShow(id), "/api/scheduling/recordappointmentnoshow");

        await ThenRefusedWith("This appointment has not been confirmed");
        ThenNoEvents();

        // The counters are left alone, which is the damage the guard actually prevents.
        // (Opening this with "And" made it an accidental step on the canvas — see bobcat#339.)
        var queue = await ThenReadModel<AppointmentsQueue>(shelterId);
        Assert.Equal(1, queue.AwaitingConfirmation);
        Assert.Equal(0, queue.Confirmed);
        Assert.Equal(0, queue.Closed);
    }

    /// <summary>
    /// Wolverine emits the 404 itself, before Validate is ever called. Nothing asserted that once,
    /// so deleting the dead null guard would have looked identical to deleting a live one.
    /// </summary>
    /// <remarks>
    /// This identity was an ORPHAN last time: the curated format could not express a refusal
    /// status, so the model had no way to declare a 404 scenario. bobcat#337 added
    /// <c>refusedWith:</c> and the model declares it now — which is also how the scaffolder knows
    /// to bind the write model non-nullable and write no guard here.
    /// </remarks>
    [Fact]
    [BobcatSlice(SliceType = typeof(ConfirmAppointment))]
    public async Task An_appointment_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new ConfirmAppointment(Guid.NewGuid()), "/api/scheduling/confirmappointment");

        ThenResponseIs(404);
        ThenNoEvents();
    }

    /// <summary>
    /// Added during review. ConfirmAppointment's guard checked only Cancelled where its four
    /// siblings checked IsClosed, so this returned 200 and appended AppointmentConfirmed to a
    /// closed stream — which also decremented a queue bucket the appointment had already left.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(ConfirmAppointment))]
    public async Task A_completed_appointment_cannot_be_confirmed()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor),
            new AppointmentCompleted(ownerId, shelterId, scheduledFor.AddHours(1)));

        await WhenPosted(new ConfirmAppointment(id), "/api/scheduling/confirmappointment");

        await ThenRefusedWith("This appointment is already closed");
        ThenNoEvents();
    }

    /// <summary>
    /// The other route into a negative count: two AppointmentConfirmed events decrement
    /// AwaitingConfirmation twice having incremented it once.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(ConfirmAppointment))]
    public async Task An_appointment_is_not_confirmed_twice()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor));

        await WhenPosted(new ConfirmAppointment(id), "/api/scheduling/confirmappointment");

        await ThenRefusedWith("This appointment is already confirmed");
        ThenNoEvents();
    }

    /// <summary>
    /// Added during review. RescheduleRequested survives a cancellation, and this guard checked
    /// only that flag — so asking to move an appointment and then cancelling it left it movable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(RescheduleAppointment))]
    public async Task A_cancelled_appointment_is_not_moved_even_when_a_move_was_asked_for()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);
        var requestedFor = new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor),
            new AppointmentRescheduleRequested(ownerId, shelterId, requestedFor, "Working that afternoon"),
            new AppointmentCancelled(ownerId, shelterId, true, "The volunteer withdrew"));

        await WhenPosted(new RescheduleAppointment(id, requestedFor), "/api/scheduling/rescheduleappointment");

        ThenResponseIs(400);
        ThenNoEvents();
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RequestReschedule))]
    public async Task A_member_asks_to_move_a_confirmed_appointment()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor));
        var requestedFor = new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

        await WhenPosted(new RequestReschedule(id, requestedFor, "Working that afternoon"),
            "/api/scheduling/requestreschedule");

        ThenEvents(typeof(AppointmentRescheduleRequested));

        var requested = TheEvent<AppointmentRescheduleRequested>();
        Assert.Equal(requestedFor, requested.RequestedFor);
        Assert.Equal("Working that afternoon", requested.Reason);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RequestReschedule))]
    public async Task A_completed_appointment_cannot_be_rescheduled()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor),
            new AppointmentCompleted(ownerId, shelterId, scheduledFor.AddHours(1)));

        await WhenPosted(
            new RequestReschedule(id, new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero), "Working that afternoon"),
            "/api/scheduling/requestreschedule");

        // Refused with "This appointment is already closed".
        ThenResponseIs(400);
        ThenNoEvents();
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RescheduleAppointment))]
    public async Task The_shelter_moves_an_appointment_a_member_asked_to_move()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);
        var requestedFor = new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor),
            new AppointmentRescheduleRequested(ownerId, shelterId, requestedFor, "Working that afternoon"));

        await WhenPosted(new RescheduleAppointment(id, requestedFor), "/api/scheduling/rescheduleappointment");

        ThenEvents(typeof(AppointmentRescheduled));
        Assert.Equal(requestedFor, TheEvent<AppointmentRescheduled>().ScheduledFor);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RescheduleAppointment))]
    public async Task An_appointment_nobody_asked_to_move_is_not_moved()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor));

        await WhenPosted(new RescheduleAppointment(id, new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero)),
            "/api/scheduling/rescheduleappointment");

        // Refused with "Nobody asked to move this appointment".
        ThenResponseIs(400);
        ThenNoEvents();
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(CompleteAppointment))]
    public async Task A_confirmed_appointment_is_completed()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor));

        await WhenPosted(new CompleteAppointment(id), "/api/scheduling/completeappointment");

        ThenEvents(typeof(AppointmentCompleted));
        Assert.Equal(ownerId, TheEvent<AppointmentCompleted>().OwnerId);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(CompleteAppointment))]
    public async Task An_appointment_nobody_confirmed_is_not_completed()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor));

        await WhenPosted(new CompleteAppointment(id), "/api/scheduling/completeappointment");

        // Refused with "This appointment has not been confirmed".
        ThenResponseIs(400);
        ThenNoEvents();
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(CancelAppointment))]
    public async Task A_confirmed_appointment_is_cancelled()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor));

        await WhenPosted(new CancelAppointment(id, "The volunteer withdrew"), "/api/scheduling/cancelappointment");

        ThenEvents(typeof(AppointmentCancelled));

        var cancelled = TheEvent<AppointmentCancelled>();
        // The flag is the point: a cancellation AFTER confirmation moves a different queue count
        // than one before it, which is what AppointmentsQueue folds on.
        Assert.True(cancelled.WasConfirmed);
        Assert.Equal("The volunteer withdrew", cancelled.Reason);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(CancelAppointment))]
    public async Task A_completed_appointment_cannot_be_cancelled()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor),
            new AppointmentCompleted(ownerId, shelterId, scheduledFor.AddHours(1)));

        await WhenPosted(new CancelAppointment(id, "The volunteer withdrew"), "/api/scheduling/cancelappointment");

        // Refused with "This appointment is already closed".
        ThenResponseIs(400);
        ThenNoEvents();
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RecordAppointmentNoShow))]
    public async Task A_no_show_is_recorded_against_a_confirmed_appointment()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor));

        await WhenPosted(new RecordAppointmentNoShow(id), "/api/scheduling/recordappointmentnoshow");

        ThenEvents(typeof(AppointmentNoShowRecorded));
        Assert.Equal(ownerId, TheEvent<AppointmentNoShowRecorded>().OwnerId);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RecordAppointmentNoShow))]
    public async Task A_completed_appointment_cannot_be_marked_a_no_show()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<Appointment>(id,
            new HomeCheckAppointmentProposed(ownerId, shelterId, AppointmentKind.HomeCheck, id, scheduledFor),
            new AppointmentConfirmed(ownerId, shelterId, scheduledFor),
            new AppointmentCompleted(ownerId, shelterId, scheduledFor.AddHours(1)));

        await WhenPosted(new RecordAppointmentNoShow(id), "/api/scheduling/recordappointmentnoshow");

        // Refused with "This appointment is already closed".
        ThenResponseIs(400);
        ThenNoEvents();
    }


    /// <summary>
    /// Wolverine's own not-found guard, which answers before Validate runs. Declared on the model
    /// (bobcat#337) rather than carried as an orphan: that declaration is what makes the write
    /// model non-nullable, which is what makes a hand-written null check unreachable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(CompleteAppointment))]
    public async Task Completing_an_appointment_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new CompleteAppointment(Guid.NewGuid()), "/api/scheduling/completeappointment");

        ThenResponseIs(404);
        ThenNoEvents();
    }

    /// <summary>
    /// Wolverine's own not-found guard, which answers before Validate runs. Declared on the model
    /// (bobcat#337) rather than carried as an orphan: that declaration is what makes the write
    /// model non-nullable, which is what makes a hand-written null check unreachable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(CancelAppointment))]
    public async Task Cancelling_an_appointment_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new CancelAppointment(Guid.NewGuid(), "The volunteer withdrew"), "/api/scheduling/cancelappointment");

        ThenResponseIs(404);
        ThenNoEvents();
    }

    /// <summary>
    /// Wolverine's own not-found guard, which answers before Validate runs. Declared on the model
    /// (bobcat#337) rather than carried as an orphan: that declaration is what makes the write
    /// model non-nullable, which is what makes a hand-written null check unreachable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(RecordAppointmentNoShow))]
    public async Task Recording_a_no_show_against_an_appointment_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new RecordAppointmentNoShow(Guid.NewGuid()), "/api/scheduling/recordappointmentnoshow");

        ThenResponseIs(404);
        ThenNoEvents();
    }

    /// <summary>
    /// Wolverine's own not-found guard, which answers before Validate runs. Declared on the model
    /// (bobcat#337) rather than carried as an orphan: that declaration is what makes the write
    /// model non-nullable, which is what makes a hand-written null check unreachable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(RequestReschedule))]
    public async Task Asking_to_move_an_appointment_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new RequestReschedule(Guid.NewGuid(), new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero), "Working that afternoon"), "/api/scheduling/requestreschedule");

        ThenResponseIs(404);
        ThenNoEvents();
    }

    /// <summary>
    /// Wolverine's own not-found guard, which answers before Validate runs. Declared on the model
    /// (bobcat#337) rather than carried as an orphan: that declaration is what makes the write
    /// model non-nullable, which is what makes a hand-written null check unreachable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(RescheduleAppointment))]
    public async Task Moving_an_appointment_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new RescheduleAppointment(Guid.NewGuid(), new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero)), "/api/scheduling/rescheduleappointment");

        ThenResponseIs(404);
        ThenNoEvents();
    }
}
