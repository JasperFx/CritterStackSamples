namespace CritterCrush.Scheduling;

/// <summary>A foster handover was proposed and awaits the foster carer's confirmation</summary>
public record FosterHandoverAppointmentProposed(Guid OwnerId, Guid ShelterId, string Kind, Guid SourceId, DateTimeOffset ScheduledFor) : IOwnerEvent, IShelterEvent;

/// <summary>
/// Automation slice: triggered by the DogPlacedInFoster event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeFosterHandoverAppointmentHandler
{
    public static StartStream Handle(DogPlacedInFoster trigger)
    {
        // The decision. Every scenario of this slice arranges no prior events, so it starts the
        // stream: mint the id (or take it off the trigger) and hand back the Appointment's first event.
        // Fill this in and delete the throw — the shape is:
        //     var id = Guid.NewGuid();   // or the identity the trigger already carries
        //     return Storage.StartStream<Appointment>(id, new FosterHandoverAppointmentProposed(/* … */));
        throw new NotImplementedException("TODO: ProposeFosterHandoverAppointment — decide which event starts the stream, and what its id is");
    }

}


