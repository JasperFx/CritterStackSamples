using Bobcat;
using Xunit;
using CritterCrush.Scheduling;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for ProposeFosterHandoverAppointment, ProposeSurrenderIntakeAppointment, ConfirmAppointment, RequestReschedule, RescheduleAppointment, CompleteAppointment, CancelAppointment, RecordAppointmentNoShow.
/// </summary>
/// <remarks>
/// Steps render from the marker comments in each test body; the verdict comes from the
/// runner. [BobcatSlice] carries the BINDING only — the slice's domain, chapter and pattern
/// are stated once, on the event model, and merge in by slice name.
/// </remarks>
[BobcatFeature("BookingAppointments")]
[Collection(CritterCrushHost.CollectionName)]
public class BookingAppointmentsSpecs(CritterCrushHost fixture) : CritterCrushSpec(fixture)
{
    [Fact]
    [BobcatSlice(SliceName = "ProposeFosterHandoverAppointment")]
    public void A_dog_placed_in_foster_proposes_a_handover()
    {
        // When DogPlacedInFoster is received (fosterApplicationId = {streamId}, ownerId = 0e5e0002-0000-0000-0000-000000000002, shelterId = 5e110001-0000-0000-0000-000000000001, proposedFor = 2026-10-02T10:30:00Z)
        // Then FosterHandoverAppointmentProposed is emitted (ownerId = 0e5e0002-0000-0000-0000-000000000002, kind = FosterHandover, sourceId = {streamId}, scheduledFor = 2026-10-02T10:30:00Z)

        throw new NotImplementedException("ProposeFosterHandoverAppointment: A dog placed in foster proposes a handover");
    }

    [Fact]
    [BobcatSlice(SliceName = "ProposeSurrenderIntakeAppointment")]
    public void A_reviewed_surrender_request_proposes_an_intake()
    {
        // When SurrenderRequestReviewed is received (surrenderRequestId = {streamId}, ownerId = 0e5e0003-0000-0000-0000-000000000003, shelterId = 5e110001-0000-0000-0000-000000000001, proposedFor = 2026-10-03T09:00:00Z)
        // Then SurrenderIntakeAppointmentProposed is emitted (ownerId = 0e5e0003-0000-0000-0000-000000000003, kind = SurrenderIntake, sourceId = {streamId}, scheduledFor = 2026-10-03T09:00:00Z)

        throw new NotImplementedException("ProposeSurrenderIntakeAppointment: A reviewed surrender request proposes an intake");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ConfirmAppointment))]
    public void A_proposed_appointment_is_confirmed()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // When ConfirmAppointment is posted to "/api/scheduling/confirmappointment" (appointmentId = {streamId})
        // Then AppointmentConfirmed is emitted (ownerId = 0e5e0001-0000-0000-0000-000000000001)

        throw new NotImplementedException("ConfirmAppointment: A proposed appointment is confirmed");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ConfirmAppointment))]
    public void A_cancelled_appointment_cannot_be_confirmed()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentCancelled (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, wasConfirmed = false, reason = The volunteer withdrew)
        // When ConfirmAppointment is posted to "/api/scheduling/confirmappointment" (appointmentId = {streamId})
        // # refused with: "This appointment was cancelled"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("ConfirmAppointment: A cancelled appointment cannot be confirmed");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ConfirmAppointment))]
    public void A_completed_appointment_cannot_be_confirmed()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Given AppointmentCompleted (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, completedAt = 2026-10-01T16:00:00Z)
        // When ConfirmAppointment is posted to "/api/scheduling/confirmappointment" (appointmentId = {streamId})
        // # refused with: "This appointment is already closed"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("ConfirmAppointment: A completed appointment cannot be confirmed");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ConfirmAppointment))]
    public void An_appointment_is_not_confirmed_twice()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // When ConfirmAppointment is posted to "/api/scheduling/confirmappointment" (appointmentId = {streamId})
        // # refused with: "This appointment is already confirmed"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("ConfirmAppointment: An appointment is not confirmed twice");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ConfirmAppointment))]
    public void An_appointment_that_does_not_exist_is_not_found()
    {
        // When ConfirmAppointment is posted to "/api/scheduling/confirmappointment" (appointmentId = {streamId})
        // # refused with: "No appointment with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("ConfirmAppointment: An appointment that does not exist is not found");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RequestReschedule))]
    public void A_member_asks_to_move_a_confirmed_appointment()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // When RequestReschedule is posted to "/api/scheduling/requestreschedule" (appointmentId = {streamId}, requestedFor = 2026-10-05T15:00:00Z, reason = Working that afternoon)
        // Then AppointmentRescheduleRequested is emitted (requestedFor = 2026-10-05T15:00:00Z, reason = Working that afternoon)

        throw new NotImplementedException("RequestReschedule: A member asks to move a confirmed appointment");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RequestReschedule))]
    public void A_completed_appointment_cannot_be_rescheduled()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Given AppointmentCompleted (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, completedAt = 2026-10-01T16:00:00Z)
        // When RequestReschedule is posted to "/api/scheduling/requestreschedule" (appointmentId = {streamId}, requestedFor = 2026-10-05T15:00:00Z, reason = Working that afternoon)
        // # refused with: "This appointment is already closed"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("RequestReschedule: A completed appointment cannot be rescheduled");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RequestReschedule))]
    public void Asking_to_move_an_appointment_that_does_not_exist_is_not_found()
    {
        // When RequestReschedule is posted to "/api/scheduling/requestreschedule" (appointmentId = {streamId}, requestedFor = 2026-10-05T15:00:00Z, reason = Working that afternoon)
        // # refused with: "No appointment with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("RequestReschedule: Asking to move an appointment that does not exist is not found");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RescheduleAppointment))]
    public void The_shelter_moves_an_appointment_a_member_asked_to_move()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Given AppointmentRescheduleRequested (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, requestedFor = 2026-10-05T15:00:00Z, reason = Working that afternoon)
        // When RescheduleAppointment is posted to "/api/scheduling/rescheduleappointment" (appointmentId = {streamId}, scheduledFor = 2026-10-05T15:00:00Z)
        // Then AppointmentRescheduled is emitted (scheduledFor = 2026-10-05T15:00:00Z)

        throw new NotImplementedException("RescheduleAppointment: The shelter moves an appointment a member asked to move");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RescheduleAppointment))]
    public void An_appointment_nobody_asked_to_move_is_not_moved()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // When RescheduleAppointment is posted to "/api/scheduling/rescheduleappointment" (appointmentId = {streamId}, scheduledFor = 2026-10-05T15:00:00Z)
        // # refused with: "Nobody asked to move this appointment"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("RescheduleAppointment: An appointment nobody asked to move is not moved");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RescheduleAppointment))]
    public void A_cancelled_appointment_is_not_moved_even_when_a_move_was_asked_for()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Given AppointmentRescheduleRequested (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, requestedFor = 2026-10-05T15:00:00Z, reason = Working that afternoon)
        // Given AppointmentCancelled (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, wasConfirmed = true, reason = The volunteer withdrew)
        // When RescheduleAppointment is posted to "/api/scheduling/rescheduleappointment" (appointmentId = {streamId}, scheduledFor = 2026-10-05T15:00:00Z)
        // # refused with: "This appointment is already closed"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("RescheduleAppointment: A cancelled appointment is not moved even when a move was asked for");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RescheduleAppointment))]
    public void Moving_an_appointment_that_does_not_exist_is_not_found()
    {
        // When RescheduleAppointment is posted to "/api/scheduling/rescheduleappointment" (appointmentId = {streamId}, scheduledFor = 2026-10-05T15:00:00Z)
        // # refused with: "No appointment with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("RescheduleAppointment: Moving an appointment that does not exist is not found");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(CompleteAppointment))]
    public void A_confirmed_appointment_is_completed()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // When CompleteAppointment is posted to "/api/scheduling/completeappointment" (appointmentId = {streamId})
        // Then AppointmentCompleted is emitted (ownerId = 0e5e0001-0000-0000-0000-000000000001)

        throw new NotImplementedException("CompleteAppointment: A confirmed appointment is completed");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(CompleteAppointment))]
    public void An_appointment_nobody_confirmed_is_not_completed()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // When CompleteAppointment is posted to "/api/scheduling/completeappointment" (appointmentId = {streamId})
        // # refused with: "This appointment has not been confirmed"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("CompleteAppointment: An appointment nobody confirmed is not completed");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(CompleteAppointment))]
    public void Completing_an_appointment_that_does_not_exist_is_not_found()
    {
        // When CompleteAppointment is posted to "/api/scheduling/completeappointment" (appointmentId = {streamId})
        // # refused with: "No appointment with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("CompleteAppointment: Completing an appointment that does not exist is not found");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(CancelAppointment))]
    public void A_confirmed_appointment_is_cancelled()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // When CancelAppointment is posted to "/api/scheduling/cancelappointment" (appointmentId = {streamId}, reason = The volunteer withdrew)
        // Then AppointmentCancelled is emitted (wasConfirmed = true, reason = The volunteer withdrew)

        throw new NotImplementedException("CancelAppointment: A confirmed appointment is cancelled");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(CancelAppointment))]
    public void A_completed_appointment_cannot_be_cancelled()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Given AppointmentCompleted (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, completedAt = 2026-10-01T16:00:00Z)
        // When CancelAppointment is posted to "/api/scheduling/cancelappointment" (appointmentId = {streamId}, reason = The volunteer withdrew)
        // # refused with: "This appointment is already closed"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("CancelAppointment: A completed appointment cannot be cancelled");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(CancelAppointment))]
    public void Cancelling_an_appointment_that_does_not_exist_is_not_found()
    {
        // When CancelAppointment is posted to "/api/scheduling/cancelappointment" (appointmentId = {streamId}, reason = The volunteer withdrew)
        // # refused with: "No appointment with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("CancelAppointment: Cancelling an appointment that does not exist is not found");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RecordAppointmentNoShow))]
    public void A_no_show_is_recorded_against_a_confirmed_appointment()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // When RecordAppointmentNoShow is posted to "/api/scheduling/recordappointmentnoshow" (appointmentId = {streamId})
        // Then AppointmentNoShowRecorded is emitted (ownerId = 0e5e0001-0000-0000-0000-000000000001)

        throw new NotImplementedException("RecordAppointmentNoShow: A no show is recorded against a confirmed appointment");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RecordAppointmentNoShow))]
    public void A_completed_appointment_cannot_be_marked_a_no_show()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // Given AppointmentConfirmed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Given AppointmentCompleted (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, completedAt = 2026-10-01T16:00:00Z)
        // When RecordAppointmentNoShow is posted to "/api/scheduling/recordappointmentnoshow" (appointmentId = {streamId})
        // # refused with: "This appointment is already closed"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("RecordAppointmentNoShow: A completed appointment cannot be marked a no show");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RecordAppointmentNoShow))]
    public void An_appointment_nobody_confirmed_is_not_a_no_show()
    {
        // Given HomeCheckAppointmentProposed (ownerId = 0e5e0001-0000-0000-0000-000000000001, shelterId = 5e110001-0000-0000-0000-000000000001, kind = HomeCheck, scheduledFor = 2026-10-01T15:00:00Z)
        // When RecordAppointmentNoShow is posted to "/api/scheduling/recordappointmentnoshow" (appointmentId = {streamId})
        // # refused with: "This appointment has not been confirmed"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("RecordAppointmentNoShow: An appointment nobody confirmed is not a no show");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RecordAppointmentNoShow))]
    public void Recording_a_no_show_against_an_appointment_that_does_not_exist_is_not_found()
    {
        // When RecordAppointmentNoShow is posted to "/api/scheduling/recordappointmentnoshow" (appointmentId = {streamId})
        // # refused with: "No appointment with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("RecordAppointmentNoShow: Recording a no show against an appointment that does not exist is not found");
    }
}
