namespace CritterCrush.Appointments;

public record HomeCheckAppointmentProposed();

/// <summary>
/// Automation slice: triggered by the HomeCheckAssignmentAccepted event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeHomeCheckAppointmentHandler
{
    public static EventsToAppend Handle(HomeCheckAssignmentAccepted trigger, [WriteModel] Appointment appointment)
    {
        // TODO: the decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        return [new HomeCheckAppointmentProposed(/* TODO */)];
    }

}


