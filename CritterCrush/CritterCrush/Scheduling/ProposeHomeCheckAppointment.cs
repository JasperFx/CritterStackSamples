namespace CritterCrush.Scheduling;

/// <summary>A home-check visit was proposed and awaits the owner's confirmation</summary>
public record HomeCheckAppointmentProposed(Guid OwnerId, Guid ShelterId, string Kind, Guid SourceId, DateTimeOffset ScheduledFor);

/// <summary>
/// Automation slice: triggered by the HomeCheckAssignmentAccepted event, never by a route. Decides and returns —
/// the framework loads the aggregate, appends, and commits.
///
/// At-least-once delivery is handled by the stream id: the appointment's stream IS the home-check assignment
/// that asked for it, so a redelivered trigger collides on StartStream instead of quietly booking
/// a second visit. What that costs — one source entity can hold only one appointment — is a
/// hotspot on the model, not a silent assumption here.
/// </summary>
public static class ProposeHomeCheckAppointmentHandler
{
    public static StartStream Handle(HomeCheckAssignmentAccepted trigger) =>
        Storage.StartStream<Appointment>(trigger.AssignmentId,
            new HomeCheckAppointmentProposed(
                trigger.OwnerId,
                trigger.ShelterId,
                AppointmentKind.HomeCheck,
                trigger.AssignmentId,
                trigger.ProposedFor));
}
