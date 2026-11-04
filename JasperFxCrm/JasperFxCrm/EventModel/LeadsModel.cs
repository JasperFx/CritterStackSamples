using JasperFx.Events.EventModeling;
using JasperFxCrm.Features.Accounts;
using JasperFxCrm.Features.Leads;

namespace JasperFxCrm.EventModel;

#region sample_leads_model
/// <summary>
/// The Leads chapter as declared. Wolverine derives a second model from the handlers and
/// endpoints as they are written; where the two disagree, `describe-event-model` says so.
/// </summary>
public class LeadsModel : EventModelDefinition
{
    public override void Configure(EventModelBuilder model)
    {
        model.InDomain("Leads").InChapter("Leads");
        model.ForAggregate<Lead>();

        model.Command<CaptureLead>()
            .TriggeredBy("A prospect submits the website contact form, or an admin enters a lead", TriggerKind.Http)
            .DeciderModel<LeadIntake>()
            .StartsStream<Lead>()
            .Emits<LeadCaptured>()            // nobody from this company or email is known
            .Emits<LeadContactAdded>()        // a colleague is already an open lead
            .Emits<LeadMessageReceived>()     // this person is already on an open lead
            .Publishes<ExistingCustomerWroteIn>() // this company is already a customer
            .Hotspot("A new message from a company whose lead was disqualified: a fresh lead, or reopen the old one?")
            .Hotspot("Can a DeciderModel slice also StartsStream? Verify against JasperFx.Events.");

        model.Command<DisqualifyLead>()
            .TriggeredBy("Admin disqualifies the lead", TriggerKind.Http)
            .Emits<LeadDisqualified>();

        model.Command<ReopenLead>()
            .TriggeredBy("Admin reopens a disqualified lead", TriggerKind.Http)
            .Emits<LeadReopened>();

        model.Command<ConvertLeadToAccount>()
            .TriggeredBy("Admin converts the lead into a customer", TriggerKind.Http)
            .Against<Lead>()
            .StartsStream<Account>()
            .Emits<LeadConvertedToAccount>()
            .Emits<AccountCreated>()
            .Emits<ContactCreated>()   // one Contact stream per person on the lead
            .Hotspot("This slice also starts one Contact stream per lead contact; EventModelBuilder keeps only the last StartsStream, so Contact is declared as an emit.");

        model.Automation("NotifyOfNewLead")
            .On<LeadCaptured>()
            .On<LeadContactAdded>()
            .On<LeadMessageReceived>()
            .ExternalSystem("Email", ExternalSystemDirection.Outbound)
            .Emits<LeadNotificationSent>();

        model.View<LeadPipelineRow>()
            .From<LeadCaptured>().From<LeadContactAdded>().From<LeadMessageReceived>()
            .From<LeadDisqualified>().From<LeadReopened>().From<LeadConvertedToAccount>();

        model.Query<Lead>("GET /api/admin/leads/{id}")
            .TriggeredBy("Admin opens a lead", TriggerKind.Http);
        model.Query<LeadPipelineRow>("GET /api/admin/leads/pipeline")
            .TriggeredBy("Admin opens the pipeline", TriggerKind.Http);
    }
}
#endregion
