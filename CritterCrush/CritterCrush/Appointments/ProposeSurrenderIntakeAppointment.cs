namespace CritterCrush.Appointments;

/// <summary>An intake appointment was proposed to the surrendering owner</summary>
public record SurrenderIntakeAppointmentProposed(Guid AppointmentId, Guid OwnerId, Guid ShelterId, Guid DogId, DateTimeOffset ProposedFor);

/// <summary>
/// Automation slice: triggered by the SurrenderRequestApproved event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits. Design for at-least-once delivery.
/// </summary>
public static class ProposeSurrenderIntakeAppointmentHandler
{
    public static StartStream Handle(SurrenderRequestApproved trigger)
    {
        // The decision. Every scenario of this slice arranges no prior events, so it starts the
        // stream: mint the id (or take it off the trigger) and hand back the Appointment's first event.
        // Fill this in and delete the throw — the shape is:
        //     var id = Guid.NewGuid();   // or the identity the trigger already carries
        //     return Storage.StartStream<Appointment>(id, new SurrenderIntakeAppointmentProposed(/* … */));
        throw new NotImplementedException("TODO: ProposeSurrenderIntakeAppointment — decide which event starts the stream, and what its id is");
    }

}


