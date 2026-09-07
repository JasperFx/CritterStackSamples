namespace CritterCrush.Appointments;

public class AppointmentsQueue
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ShelterId { get; set; }
    public Guid DogId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ScheduledFor { get; set; }
    public bool AwaitingAction { get; set; }
}


// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
public class AppointmentsQueueProjection : SingleStreamProjection<AppointmentsQueue, Guid>
{

    public void Apply(HomeCheckAppointmentProposed homeCheckAppointmentProposed, AppointmentsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: AppointmentsQueue — project HomeCheckAppointmentProposed");
    }


    public void Apply(FosterHandoverAppointmentProposed fosterHandoverAppointmentProposed, AppointmentsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: AppointmentsQueue — project FosterHandoverAppointmentProposed");
    }


    public void Apply(SurrenderIntakeAppointmentProposed surrenderIntakeAppointmentProposed, AppointmentsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: AppointmentsQueue — project SurrenderIntakeAppointmentProposed");
    }


    public void Apply(AppointmentConfirmed appointmentConfirmed, AppointmentsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: AppointmentsQueue — project AppointmentConfirmed");
    }


    public void Apply(RescheduleRequested rescheduleRequested, AppointmentsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: AppointmentsQueue — project RescheduleRequested");
    }


    public void Apply(AppointmentRescheduled appointmentRescheduled, AppointmentsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: AppointmentsQueue — project AppointmentRescheduled");
    }


    public void Apply(AppointmentCompleted appointmentCompleted, AppointmentsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: AppointmentsQueue — project AppointmentCompleted");
    }


    public void Apply(AppointmentCancelled appointmentCancelled, AppointmentsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: AppointmentsQueue — project AppointmentCancelled");
    }


    public void Apply(AppointmentNoShowRecorded appointmentNoShowRecorded, AppointmentsQueue view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: AppointmentsQueue — project AppointmentNoShowRecorded");
    }

}


public static class GetAppointmentsQueueEndpoint
{
    [WolverineGet("/api/appointmentsqueue/{id}")]
    public static Task<AppointmentsQueue?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<AppointmentsQueue>(id, ct);
}


