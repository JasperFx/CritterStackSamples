namespace CritterCrush.Appointments;

/// <summary>A home-check visit was proposed to the owner</summary>
public record HomeCheckAppointmentProposed(Guid AppointmentId, Guid OwnerId, Guid ShelterId, Guid DogId, Guid VolunteerId, DateTimeOffset ProposedFor);

/// <summary>
/// Automation slice: triggered by the HomeCheckAssignmentAccepted event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeHomeCheckAppointmentHandler
{
    public static EventsToAppend Handle(HomeCheckAssignmentAccepted trigger, [WriteModel] Appointment appointment)
    {
        // The decision. Nothing to append is `return [];` — never a nullable event (wolverine#4309).
        // Fill this in and delete the throw — the shape is:
        //     return [new HomeCheckAppointmentProposed(/* … */)];
        throw new NotImplementedException("TODO: ProposeHomeCheckAppointment — decide which events this slice appends");
    }

}


