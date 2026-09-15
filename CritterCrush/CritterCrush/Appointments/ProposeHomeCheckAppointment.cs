namespace CritterCrush.Appointments;

/// <summary>A home-check visit was proposed to the owner</summary>
public record HomeCheckAppointmentProposed(Guid AppointmentId, Guid OwnerId, Guid ShelterId, Guid DogId, Guid VolunteerId, DateTimeOffset ProposedFor);

/// <summary>
/// Automation slice: triggered by the HomeCheckAssignmentAccepted event, never by a route. Decides and returns —
/// the framework starts the stream, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeHomeCheckAppointmentHandler
{
    /// <summary>
    /// The Appointment stream takes the identity the trigger already carries: one accepted
    /// assignment is one appointment, so the id is the assignment's. That is what makes the
    /// automation safe under at-least-once delivery — a redelivered trigger addresses the
    /// stream it already started, and the store refuses the second start instead of quietly
    /// booking the same visit twice under a minted id.
    /// </summary>
    public static StartStream Handle(HomeCheckAssignmentAccepted trigger)
    {
        var appointmentId = trigger.AssignmentId;

        return Storage.StartStream<Appointment>(appointmentId, new HomeCheckAppointmentProposed(
            appointmentId,
            trigger.OwnerId,
            trigger.ShelterId,
            trigger.DogId,
            trigger.VolunteerId,
            trigger.ProposedFor));
    }
}
