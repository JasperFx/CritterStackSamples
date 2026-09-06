namespace CritterCrush.Appointments;

public record SurrenderIntakeAppointmentProposed();

/// <summary>
/// Automation slice: triggered by the SurrenderRequestApproved event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeSurrenderIntakeAppointmentHandler
{
    public static EventsToAppend Handle(SurrenderRequestApproved trigger, [WriteModel] Appointment appointment)
    {
        // TODO: the decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        return [new SurrenderIntakeAppointmentProposed(/* TODO */)];
    }

}


