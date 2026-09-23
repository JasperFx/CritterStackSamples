namespace CritterCrush.Scheduling;

/// <summary>
/// Automation slice: triggered by the SurrenderRequestReviewed event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeSurrenderIntakeAppointmentHandler
{
    public static StartStream Handle(SurrenderRequestReviewed trigger)
    {
        // The appointment's stream IS SurrenderRequestId. Not a fresh Guid: an id the
        // automation invents is an id no scenario can predict, so nothing could ever assert WHERE
        // the event landed (bobcat#319/#360). It is safe to reuse only because that id belongs to
        // no other aggregate in this model — Marten's stream id space is global across types.
        return Storage.StartStream<Appointment>(trigger.SurrenderRequestId, new SurrenderIntakeAppointmentProposed(
            trigger.OwnerId,
            trigger.ShelterId,
            AppointmentKind.SurrenderIntake,
            trigger.SurrenderRequestId,
            trigger.ProposedFor));
    }

}
