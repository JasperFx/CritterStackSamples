namespace CritterCrush.Appointments;

public record FosterHandoverAppointmentProposed();

/// <summary>
/// Automation slice: triggered by the FosterPlacementApproved event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeFosterHandoverAppointmentHandler
{
    public static EventsToAppend Handle(FosterPlacementApproved trigger, [WriteModel] Appointment appointment)
    {
        // TODO: the decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        return [new FosterHandoverAppointmentProposed(/* TODO */)];
    }

}


