namespace CritterCrush.Appointments;

public class MyAppointments
{
    public Guid Id { get; set; }
    // TODO: the projected columns the model's scenarios assert on
}


// Async lifecycle: register with the daemon RUNNING (AddAsyncDaemon), or this never advances.
public class MyAppointmentsProjection : MultiStreamProjection<MyAppointments, Guid>
{
    public MyAppointmentsProjection()
    {
        // The slicing rule, without which this projection cannot be registered. One document
        // per key; Identities<T>(x => [x.OneId, x.OtherId]) where one event updates several.
        Identity<HomeCheckAppointmentProposed>(x => x.AppointmentId);
        Identity<FosterHandoverAppointmentProposed>(x => x.AppointmentId);
        Identity<SurrenderIntakeAppointmentProposed>(x => x.AppointmentId);
        Identity<AppointmentConfirmed>(x => x.AppointmentId);
        Identity<RescheduleRequested>(x => x.AppointmentId);
        Identity<AppointmentRescheduled>(x => x.AppointmentId);
        Identity<AppointmentCompleted>(x => x.AppointmentId);
        Identity<AppointmentCancelled>(x => x.AppointmentId);
        Identity<AppointmentNoShowRecorded>(x => x.AppointmentId);
    }


    public void Apply(HomeCheckAppointmentProposed homeCheckAppointmentProposed, MyAppointments view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: MyAppointments — project HomeCheckAppointmentProposed");
    }


    public void Apply(FosterHandoverAppointmentProposed fosterHandoverAppointmentProposed, MyAppointments view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: MyAppointments — project FosterHandoverAppointmentProposed");
    }


    public void Apply(SurrenderIntakeAppointmentProposed surrenderIntakeAppointmentProposed, MyAppointments view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: MyAppointments — project SurrenderIntakeAppointmentProposed");
    }


    public void Apply(AppointmentConfirmed appointmentConfirmed, MyAppointments view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: MyAppointments — project AppointmentConfirmed");
    }


    public void Apply(RescheduleRequested rescheduleRequested, MyAppointments view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: MyAppointments — project RescheduleRequested");
    }


    public void Apply(AppointmentRescheduled appointmentRescheduled, MyAppointments view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: MyAppointments — project AppointmentRescheduled");
    }


    public void Apply(AppointmentCompleted appointmentCompleted, MyAppointments view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: MyAppointments — project AppointmentCompleted");
    }


    public void Apply(AppointmentCancelled appointmentCancelled, MyAppointments view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: MyAppointments — project AppointmentCancelled");
    }


    public void Apply(AppointmentNoShowRecorded appointmentNoShowRecorded, MyAppointments view)
    {
        // Fill this in and delete the throw — the model's scenarios say what the view holds.
        // Until then the projection stops on this event, so a scenario asserting the read model
        // fails on its projection wait rather than on a value.
        throw new NotImplementedException("TODO: MyAppointments — project AppointmentNoShowRecorded");
    }

}


public static class GetMyAppointmentsEndpoint
{
    [WolverineGet("/api/myappointments/{id}")]
    public static Task<MyAppointments?> Get(Guid id, IQuerySession session, CancellationToken ct)
        => session.LoadAsync<MyAppointments>(id, ct);
}


