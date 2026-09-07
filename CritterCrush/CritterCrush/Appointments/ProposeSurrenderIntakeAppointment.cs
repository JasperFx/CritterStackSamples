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
        // One approved surrender request proposes exactly one intake appointment, so the request's
        // own identity names the Appointment stream. That keeps the decision deterministic: a
        // redelivered trigger targets the same stream instead of quietly minting a second
        // appointment for the same surrender.
        var appointmentId = trigger.RequestId;

        return Storage.StartStream<Appointment>(appointmentId, new SurrenderIntakeAppointmentProposed(
            appointmentId,
            trigger.OwnerId,
            trigger.ShelterId,
            trigger.DogId,
            trigger.ProposedFor));
    }
}
