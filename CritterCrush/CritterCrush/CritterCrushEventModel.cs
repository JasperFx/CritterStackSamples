using JasperFx.Events.EventModeling;

namespace CritterCrush;

// The declared Event Model, in C#. This used to be models/CritterCrush.emodel.yaml registered with
// Bobcat.EventModel's AddEventModelFile; Bobcat retired the curated YAML format as an authoring surface
// (bobcat#406) and AddEventModelFile with it, so the slice-level half of that file — pattern, chapter,
// trigger, aggregate, events, read models, external systems and hotspots — is ported here onto
// JasperFx.Events' EventModelDefinition and registered in Program.cs with AddEventModel<T>().
//
// What did NOT come across is the scaffolding input (field hints and example scenarios): that is not
// part of the runtime model, and it stays in the YAML for models/Scaffolder (see models/README.md).
//
// Both feed the same merged EventModelDescriptor as the model Wolverine and Marten derive from the code,
// on the Declared rung: where they disagree the merge says so.

/// <summary>Scheduling: the BookingAppointments chapter.</summary>
public class BookingAppointmentsModel : EventModelDefinition
{
    public override void Configure(EventModelBuilder model)
    {
        model.InChapter("BookingAppointments");
        model.InDomain("Scheduling");
        model.ForAggregate<Appointment>();

        // ---- Proposals: three automations, each STARTING an Appointment stream whose id is the
        // triggering entity's, so a redelivered trigger collides on StartStream instead of booking twice.
        model.Automation("ProposeHomeCheckAppointment")
            .TriggeredBy("HomeCheckAssignmentAccepted", TriggerKind.MessageHandler)
            .On<HomeCheckAssignmentAccepted>()
            .HandledBy(typeof(ProposeHomeCheckAppointmentHandler).Name)
            .StartsStream<Appointment>()
            .Emits<HomeCheckAppointmentProposed>();

        model.Automation("ProposeFosterHandoverAppointment")
            .TriggeredBy("DogPlacedInFoster", TriggerKind.MessageHandler)
            .On<DogPlacedInFoster>()
            .HandledBy(typeof(ProposeFosterHandoverAppointmentHandler).Name)
            .ExternalSystem("Fostering", ExternalSystemDirection.Inbound, "")
            .StartsStream<Appointment>()
            .Emits<FosterHandoverAppointmentProposed>();

        model.Automation("ProposeSurrenderIntakeAppointment")
            .TriggeredBy("SurrenderRequestReviewed", TriggerKind.MessageHandler)
            .On<SurrenderRequestReviewed>()
            .HandledBy(typeof(ProposeSurrenderIntakeAppointmentHandler).Name)
            .ExternalSystem("Surrenders", ExternalSystemDirection.Inbound, "")
            .StartsStream<Appointment>()
            .Emits<SurrenderIntakeAppointmentProposed>()
            .Hotspot("One source entity can hold only one appointment, because the source id IS the appointment's "
                     + "stream id. A surrender needing a second intake visit (the first one a no-show, say) has nowhere "
                     + "to put it. The alternative is a minted id, which costs idempotency on redelivery; this model chose "
                     + "idempotency and wrote the cost down.")
            .Hotspot("The board's trigger is \"Surrender Request Reviewed\", and a review can plainly end either way, but "
                     + "the board declares no outcome field, so this model cannot say whether a REJECTED review also books "
                     + "an intake. Today every reviewed request books one.");

        // ---- The appointment's own life: collapsed HTTP endpoints deciding against Appointment
        model.Command<ConfirmAppointment>()
            .TriggeredBy("Confirm Appointment", TriggerKind.Http)
            .Emits<AppointmentConfirmed>();

        model.Command<RequestReschedule>()
            .TriggeredBy("Request Reschedule", TriggerKind.Http)
            .Emits<AppointmentRescheduleRequested>();

        model.Command<RescheduleAppointment>()
            .TriggeredBy("Reschedule Appointment", TriggerKind.Http)
            .Emits<AppointmentRescheduled>()
            .Hotspot("The board draws Reschedule only downstream of Request Reschedule, so this model refuses a move "
                     + "nobody asked for. Whether the shelter may move an appointment unilaterally is undecided.");

        model.Command<CompleteAppointment>()
            .TriggeredBy("Complete Appointment", TriggerKind.Http)
            .Emits<AppointmentCompleted>();

        model.Command<CancelAppointment>()
            .TriggeredBy("Cancel Appointment", TriggerKind.Http)
            .Emits<AppointmentCancelled>();

        model.Command<RecordAppointmentNoShow>()
            .TriggeredBy("Record Appointment No-Show", TriggerKind.Http)
            .Emits<AppointmentNoShowRecorded>();

        // ---- Two multi-stream (fan-out) views over every appointment stream
        model.View<AppointmentsQueue>()
            .TriggeredBy("Appointments Queue", TriggerKind.Http)
            .AsMultiStream()
            .Projects<AppointmentsQueueProjection>()
            .From<HomeCheckAppointmentProposed>()
            .From<FosterHandoverAppointmentProposed>()
            .From<SurrenderIntakeAppointmentProposed>()
            .From<AppointmentConfirmed>()
            .From<AppointmentCompleted>()
            .From<AppointmentCancelled>()
            .From<AppointmentNoShowRecorded>();

        model.View<MyAppointments>()
            .TriggeredBy("My Appointments", TriggerKind.Http)
            .AsMultiStream()
            .Projects<MyAppointmentsProjection>()
            .From<HomeCheckAppointmentProposed>()
            .From<FosterHandoverAppointmentProposed>()
            .From<SurrenderIntakeAppointmentProposed>()
            .From<AppointmentConfirmed>()
            .From<AppointmentCompleted>()
            .From<AppointmentCancelled>()
            .From<AppointmentNoShowRecorded>();
    }
}

/// <summary>Volunteering: the VolunteeringAndHomeChecks chapter.</summary>
public class VolunteeringAndHomeChecksModel : EventModelDefinition
{
    public override void Configure(EventModelBuilder model)
    {
        model.InChapter("VolunteeringAndHomeChecks");
        model.InDomain("Volunteering");

        model.Command<ApplyToVolunteer>()
            .TriggeredBy("Volunteer Program", TriggerKind.Http)
            .StartsStream<VolunteerApplication>()
            .Emits<VolunteerApplicationSubmitted>()
            .Hotspot("The board lists areas of interest as a set a person picks from, while its own sample row shows a "
                     + "single value. This model carries one area; if it is really a set, it belongs on the event as a set.")
            .Hotspot("One application per person, because the applicant's id IS the application's stream. Someone "
                     + "rejected in March and re-applying in September has nowhere to put the second application.");

        model.Command<ReviewVolunteerApplication>()
            .TriggeredBy("Volunteer Applications Queue", TriggerKind.Http)
            .Against<VolunteerApplication>()
            .Emits<VolunteerApplicationReviewed>();

        model.Command<ApproveVolunteer>()
            .TriggeredBy("Approve Volunteer", TriggerKind.Http)
            .Against<VolunteerApplication>()
            .Emits<VolunteerApproved>();

        model.Command<RejectVolunteerApplication>()
            .TriggeredBy("Reject Volunteer Application", TriggerKind.Http)
            .Against<VolunteerApplication>()
            .Emits<VolunteerApplicationRejected>();

        model.Command<RequestHomeCheck>()
            .TriggeredBy("Request Home Check", TriggerKind.Http)
            .ExternalSystem("ShelterAdoption", ExternalSystemDirection.Inbound, "")
            .StartsStream<HomeCheck>()
            .Emits<HomeCheckRequested>()
            .Hotspot("The board arranges \"Application Reviewed\" (owned by ShelterReviewsApplication) before this "
                     + "command as context rather than as a rule, so applicationId is carried as data and nothing here "
                     + "enforces that the adoption application was reviewed.");

        model.Command<AcceptHomeCheckAssignment>()
            .TriggeredBy("Home Check Assigned", TriggerKind.Http)
            .Against<HomeCheck>()
            .Emits<HomeCheckAssignmentAccepted>()
            .Hotspot("The board arranges both \"Home Check Requested\" and \"Volunteer Approved\" before this command. "
                     + "\"Only an approved volunteer may accept\" is deliberately NOT enforced (bobcat#320).");

        model.Command<SubmitHomeCheckReport>()
            .TriggeredBy("Submit Home Check Report", TriggerKind.Http)
            .ExternalSystem("ShelterAdoption", ExternalSystemDirection.Outbound, "")
            .Against<HomeCheck>()
            .Emits<HomeCheckReportSubmitted>();

        model.View<VolunteerApplicationsQueue>()
            .TriggeredBy("Volunteer Applications Queue", TriggerKind.Http)
            .AsSingleStream()
            .Projects<VolunteerApplicationsQueueProjection>()
            .From<VolunteerApplicationSubmitted>()
            .From<VolunteerApplicationReviewed>()
            .From<VolunteerApproved>()
            .From<VolunteerApplicationRejected>();
    }
}
