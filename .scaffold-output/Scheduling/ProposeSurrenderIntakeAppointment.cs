namespace CritterCrush.Scheduling;

/// <summary>A surrender intake was proposed and awaits the owner's confirmation</summary>
public record SurrenderIntakeAppointmentProposed(Guid OwnerId, Guid ShelterId, string Kind, Guid SourceId, DateTimeOffset ScheduledFor) : IOwnerEvent, IShelterEvent;

/// <summary>
/// Automation slice: triggered by the SurrenderRequestReviewed event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeSurrenderIntakeAppointmentHandler
{
    public static StartStream Handle(SurrenderRequestReviewed trigger)
    {
        // HOTSPOT (from the model): One source entity can hold only one appointment, because the source id IS the appointment's stream id. A surrender needing a second intake visit — the first one a no-show, say — has nowhere to put it. The alternative is a minted id, which costs idempotency on redelivery; this model chose idempotency and wrote the cost down.

        // HOTSPOT (from the model): The board's trigger is "Surrender Request Reviewed", and a review can plainly end either way — but the board declares no outcome field, so this model cannot say whether a REJECTED review also books an intake. Today every reviewed request books one. Resolving it means asking the Surrenders chapter to carry the outcome on its contract, which is a change to somebody else's event, not to this slice.

        // The decision. Every scenario of this slice arranges no prior events, so it starts the
        // stream: mint the id (or take it off the trigger) and hand back the Appointment's first event.
        // Fill this in and delete the throw — the shape is:
        //     var id = Guid.NewGuid();   // or the identity the trigger already carries
        //     return Storage.StartStream<Appointment>(id, new SurrenderIntakeAppointmentProposed(/* … */));
        throw new NotImplementedException("TODO: ProposeSurrenderIntakeAppointment — decide which event starts the stream, and what its id is");
    }

}


