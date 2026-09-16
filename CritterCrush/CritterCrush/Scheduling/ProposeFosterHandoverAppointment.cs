namespace CritterCrush.Scheduling;

/// <summary>A foster handover was proposed and awaits the foster carer's confirmation</summary>
public record FosterHandoverAppointmentProposed(Guid OwnerId, Guid ShelterId, string Kind, Guid SourceId, DateTimeOffset ScheduledFor);

/// <summary>
/// Automation slice: triggered by the DogPlacedInFoster event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits.
///
/// At-least-once delivery is handled by the stream id: the appointment's stream IS the foster application
/// that asked for it, so a redelivered trigger collides on StartStream instead of quietly booking
/// a second visit. What that costs — one source entity can hold only one appointment — is a
/// hotspot on the model, not a silent assumption here.
/// </summary>
public static class ProposeFosterHandoverAppointmentHandler
{
    public static StartStream Handle(DogPlacedInFoster trigger) =>
        Storage.StartStream<Appointment>(trigger.FosterApplicationId,
            new FosterHandoverAppointmentProposed(
                trigger.OwnerId,
                trigger.ShelterId,
                AppointmentKind.FosterHandover,
                trigger.FosterApplicationId,
                trigger.ProposedFor));
}
