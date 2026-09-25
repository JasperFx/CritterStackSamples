namespace CritterCrush.Scheduling;

/// <summary>
/// Automation slice: triggered by the HomeCheckAssignmentAccepted event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeHomeCheckAppointmentHandler
{
    public static StartStream Handle(HomeCheckAssignmentAccepted trigger)
    {
        // The appointment's stream IS AssignmentId. Not a fresh Guid: an id the
        // automation invents is an id no scenario can predict, so nothing could ever assert WHERE
        // the event landed (bobcat#319/#360). It is safe to reuse only because that id belongs to
        // no other aggregate in this model — Marten's stream id space is global across types.
        return Storage.StartStream<Appointment>(trigger.AssignmentId, new HomeCheckAppointmentProposed(
            trigger.OwnerId,
            trigger.ShelterId,
            AppointmentKind.HomeCheck,
            trigger.AssignmentId,
            trigger.ProposedFor));
    }

}
