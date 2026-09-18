namespace CritterCrush.Scheduling;

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
