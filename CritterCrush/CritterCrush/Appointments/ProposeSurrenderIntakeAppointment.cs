namespace CritterCrush.Appointments;

/// <summary>An intake appointment was proposed to the surrendering owner</summary>
public record SurrenderIntakeAppointmentProposed(Guid AppointmentId, Guid OwnerId, Guid ShelterId, Guid DogId, DateTimeOffset ProposedFor);

/// <summary>
/// Automation slice: triggered by the SurrenderRequestApproved event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeSurrenderIntakeAppointmentHandler
{
    public static EventsToAppend Handle(SurrenderRequestApproved trigger, [WriteModel] Appointment appointment)
    {
        // The decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        // Fill this in and delete the throw — the shape is:
        //     return [new SurrenderIntakeAppointmentProposed(/* … */)];
        throw new NotImplementedException("TODO: ProposeSurrenderIntakeAppointment — decide which events this slice appends");
    }

}


