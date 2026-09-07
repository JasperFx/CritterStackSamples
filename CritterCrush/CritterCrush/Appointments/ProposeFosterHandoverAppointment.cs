namespace CritterCrush.Appointments;

/// <summary>A handover between the shelter and the foster was proposed</summary>
public record FosterHandoverAppointmentProposed(Guid AppointmentId, Guid OwnerId, Guid ShelterId, Guid DogId, Guid FosterId, DateTimeOffset ProposedFor);

/// <summary>
/// Automation slice: triggered by the FosterPlacementApproved event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeFosterHandoverAppointmentHandler
{
    public static EventsToAppend Handle(FosterPlacementApproved trigger, [WriteModel] Appointment appointment)
    {
        // The decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        // Fill this in and delete the throw — the shape is:
        //     return [new FosterHandoverAppointmentProposed(/* … */)];
        throw new NotImplementedException("TODO: ProposeFosterHandoverAppointment — decide which events this slice appends");
    }

}


