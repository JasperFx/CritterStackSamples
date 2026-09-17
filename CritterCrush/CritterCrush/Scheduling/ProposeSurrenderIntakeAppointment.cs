namespace CritterCrush.Scheduling;

/// <summary>A surrender intake was proposed and awaits the owner's confirmation</summary>
public record SurrenderIntakeAppointmentProposed(Guid OwnerId, Guid ShelterId, string Kind, Guid SourceId, DateTimeOffset ScheduledFor) : IShelterEvent, IOwnerEvent;

/// <inheritdoc cref="ProposeHomeCheckAppointmentHandler"/>
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
