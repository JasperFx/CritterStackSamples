namespace CritterCrush.Scheduling;

/// <summary>A surrender intake was proposed and awaits the owner's confirmation</summary>
public record SurrenderIntakeAppointmentProposed(Guid OwnerId, Guid ShelterId, string Kind, Guid SourceId, DateTimeOffset ScheduledFor);

/// <summary>
/// Automation slice: triggered by the SurrenderRequestReviewed event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits.
///
/// At-least-once delivery is handled by the stream id: the appointment's stream IS the surrender request
/// that asked for it, so a redelivered trigger collides on StartStream instead of quietly booking
/// a second visit. What that costs — one source entity can hold only one appointment — is a
/// hotspot on the model, not a silent assumption here.
/// </summary>
/// <remarks>
/// Every reviewed request books an intake, because the Surrenders contract carries no outcome. The
/// model records that as a hotspot rather than guessing a field that chapter never declared.
/// </remarks>
public static class ProposeSurrenderIntakeAppointmentHandler
{
    public static StartStream Handle(SurrenderRequestReviewed trigger) =>
        Storage.StartStream<Appointment>(trigger.SurrenderRequestId,
            new SurrenderIntakeAppointmentProposed(
                trigger.OwnerId,
                trigger.ShelterId,
                AppointmentKind.SurrenderIntake,
                trigger.SurrenderRequestId,
                trigger.ProposedFor));
}
