namespace CritterCrush.Scheduling;

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
