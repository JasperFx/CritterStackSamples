namespace CritterCrush.Appointments;

/// <summary>A handover between the shelter and the foster was proposed</summary>
public record FosterHandoverAppointmentProposed(Guid AppointmentId, Guid OwnerId, Guid ShelterId, Guid DogId, Guid FosterId, DateTimeOffset ProposedFor);

/// <summary>
/// Automation slice: triggered by the FosterPlacementApproved event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Designed for at-least-once delivery:
/// one approved placement is exactly one handover appointment, so the appointment takes the
/// placement's identity. A redelivered FosterPlacementApproved then finds the stream it already
/// started and cleanly does nothing, rather than proposing a second handover.
/// </summary>
public static class ProposeFosterHandoverAppointmentHandler
{
    public static EventsToAppend Handle(
        FosterPlacementApproved trigger,
        [WriteModel(nameof(FosterPlacementApproved.PlacementId))] Appointment? existing)
    {
        if (existing is not null) return [];

        return
        [
            new FosterHandoverAppointmentProposed(
                trigger.PlacementId,
                trigger.OwnerId,
                trigger.ShelterId,
                trigger.DogId,
                trigger.FosterId,
                trigger.ProposedFor)
        ];
    }
}
