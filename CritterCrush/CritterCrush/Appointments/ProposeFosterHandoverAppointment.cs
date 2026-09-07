namespace CritterCrush.Appointments;

/// <summary>A handover between the shelter and the foster was proposed</summary>
public record FosterHandoverAppointmentProposed(Guid AppointmentId, Guid OwnerId, Guid ShelterId, Guid DogId, Guid FosterId, DateTimeOffset ProposedFor);

/// <summary>
/// Automation slice: triggered by the FosterPlacementApproved event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeFosterHandoverAppointmentHandler
{
    public static StartStream Handle(FosterPlacementApproved trigger)
    {
        // The decision. Every scenario of this slice arranges no prior events, so it starts the
        // stream: mint the id (or take it off the trigger) and hand back the Appointment's first event.
        // Fill this in and delete the throw — the shape is:
        //     var id = Guid.NewGuid();   // or the identity the trigger already carries
        //     return Storage.StartStream<Appointment>(id, new FosterHandoverAppointmentProposed(/* … */));
        throw new NotImplementedException("TODO: ProposeFosterHandoverAppointment — decide which event starts the stream, and what its id is");
    }

}


