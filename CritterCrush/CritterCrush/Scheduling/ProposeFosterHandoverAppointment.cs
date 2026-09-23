namespace CritterCrush.Scheduling;

/// <summary>
/// Automation slice: triggered by the DogPlacedInFoster event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeFosterHandoverAppointmentHandler
{
    public static StartStream Handle(DogPlacedInFoster trigger)
    {
        // The appointment's stream IS FosterApplicationId. Not a fresh Guid: an id the
        // automation invents is an id no scenario can predict, so nothing could ever assert WHERE
        // the event landed (bobcat#319/#360). It is safe to reuse only because that id belongs to
        // no other aggregate in this model — Marten's stream id space is global across types.
        return Storage.StartStream<Appointment>(trigger.FosterApplicationId, new FosterHandoverAppointmentProposed(
            trigger.OwnerId,
            trigger.ShelterId,
            AppointmentKind.FosterHandover,
            trigger.FosterApplicationId,
            trigger.ProposedFor));
    }

}
