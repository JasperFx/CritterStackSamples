namespace CritterCrush.Appointments;

/// <summary>A home-check visit was proposed to the owner</summary>
public record HomeCheckAppointmentProposed(Guid AppointmentId, Guid OwnerId, Guid ShelterId, Guid DogId, Guid VolunteerId, DateTimeOffset ProposedFor);

/// <summary>
/// Automation slice: triggered by the HomeCheckAssignmentAccepted event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeHomeCheckAppointmentHandler
{
    public static StartStream Handle(HomeCheckAssignmentAccepted trigger)
    {
        // The decision. Every scenario of this slice arranges no prior events, so it starts the
        // stream: mint the id (or take it off the trigger) and hand back the Appointment's first event.
        // Fill this in and delete the throw — the shape is:
        //     var id = Guid.NewGuid();   // or the identity the trigger already carries
        //     return Storage.StartStream<Appointment>(id, new HomeCheckAppointmentProposed(/* … */));
        throw new NotImplementedException("TODO: ProposeHomeCheckAppointment — decide which event starts the stream, and what its id is");
    }

}


