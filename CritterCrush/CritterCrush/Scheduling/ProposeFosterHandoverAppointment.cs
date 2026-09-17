namespace CritterCrush.Scheduling;

/// <summary>A foster handover was proposed and awaits the foster carer's confirmation</summary>
public record FosterHandoverAppointmentProposed(Guid OwnerId, Guid ShelterId, string Kind, Guid SourceId, DateTimeOffset ScheduledFor) : IShelterEvent, IOwnerEvent;

/// <inheritdoc cref="ProposeHomeCheckAppointmentHandler"/>
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
