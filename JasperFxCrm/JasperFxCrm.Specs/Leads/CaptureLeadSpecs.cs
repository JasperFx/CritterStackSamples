namespace JasperFxCrm.Specs.Leads;

#region sample_capture_lead_specs
[BobcatFeature("Capturing leads from the website")]
public sealed class CapturingLeads(CrmFixture app) : CrmSpec(app)
{
    private static readonly DateTimeOffset Yesterday = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static LeadCaptured AcmeWroteIn(Guid leadId) => new(
        leadId, LeadSource.WebForm,
        new LeadContact("Jane Doe", "jane@acme.com", "jane@acme.com"),
        "Acme", "acme.com", "We are evaluating Marten for a new system.",
        [Offering.Marten], Yesterday, By: null);

    private static CaptureLead Submission(string name, string email, string message, params Offering[] interests) =>
        new(LeadSource.WebForm, name, email, CompanyName: null, message, interests);

    [BobcatSpec(typeof(CaptureLead), Domain = "Leads")]
    public async Task the_first_message_from_a_new_company_starts_a_lead()
    {
        await WhenPosted(Submission("Jane Doe", "Jane@Acme.com", "We are evaluating Marten.", Offering.Marten), "/api/leads");

        ThenResponseIs(202);
        ThenEvents(Specify<LeadCaptured>()
            .With(x => x.Source, LeadSource.WebForm)
            .With(x => x.CompanyDomain, "acme.com")
            .With(x => x.Contact, new LeadContact("Jane Doe", "Jane@Acme.com", "jane@acme.com"))
            .With(x => x.Interests, Expect.Exactly(Offering.Marten)));
    }

    [BobcatSpec(typeof(CaptureLead), Domain = "Leads")]
    public async Task a_colleague_from_the_same_company_joins_the_open_lead()
    {
        var leadId = Guid.CreateVersion7();
        await GivenEvents<Lead>(leadId, AcmeWroteIn(leadId));

        await WhenPosted(Submission("Bob Roe", "bob@acme.com", "Jane asked me to follow up on Wolverine.", Offering.Wolverine), "/api/leads");

        ThenResponseIs(202);
        ThenEvents(Specify<LeadContactAdded>()
            .With(x => x.LeadId, leadId)
            .With(x => x.Contact, new LeadContact("Bob Roe", "bob@acme.com", "bob@acme.com")));
        await ThenAggregate<Lead>(leadId, Specify<Lead>()
            .With(x => x.Status, LeadStatus.Open)
            .With(x => x.Interests, Expect.InAnyOrder(Offering.Marten, Offering.Wolverine)));
    }

    [BobcatSpec(typeof(CaptureLead), Domain = "Leads")]
    public async Task the_same_person_writing_again_is_recorded_on_their_lead()
    {
        var leadId = Guid.CreateVersion7();
        await GivenEvents<Lead>(leadId, AcmeWroteIn(leadId));

        await WhenPosted(Submission("Jane Doe", "jane+crm@acme.com", "Any update?"), "/api/leads");

        ThenEvents(Specify<LeadMessageReceived>()
            .With(x => x.LeadId, leadId)
            .With(x => x.FromNormalizedEmail, "jane@acme.com")
            .With(x => x.Message, "Any update?"));
    }

    [BobcatSpec(typeof(CaptureLead), Domain = "Leads")]
    public async Task two_people_on_free_mail_are_two_leads()
    {
        var leadId = Guid.CreateVersion7();
        await GivenEvents<Lead>(leadId, AcmeWroteIn(leadId) with
        {
            Contact = new LeadContact("Jane Doe", "jane.doe@gmail.com", "janedoe@gmail.com"),
            CompanyName = null,
            CompanyDomain = null
        });

        await WhenPosted(Submission("Bob Roe", "bob.roe@gmail.com", "Hi"), "/api/leads");

        ThenEvents(Specify<LeadCaptured>()
            .With(x => x.CompanyDomain, (string?)null)
            .With(x => x.Contact, new LeadContact("Bob Roe", "bob.roe@gmail.com", "bobroe@gmail.com")));
    }

    [BobcatSpec(typeof(CaptureLead), Domain = "Leads")]
    public async Task a_message_from_an_existing_customer_is_a_touchpoint_on_their_account()
    {
        var accountId = Guid.CreateVersion7();
        var contactId = Guid.CreateVersion7();
        await GivenEvents<Account>(accountId,
            new AccountCreated(accountId, "jane@acme.com", "acme.com", AccountSource.Checkout, AccountKind.Organization, "Acme"));
        await GivenEventsOn<Contact>(contactId,
            new ContactCreated(contactId, accountId, "jane@acme.com", "Jane Doe", IsPrimary: true));

        await WhenPosted(Submission("Bob Roe", "bob@acme.com", "We need another seat."), "/api/leads");

        ThenResponseIs(202);
        ThenNoEvents();
        ThenMessageSent(Specify<ExistingCustomerWroteIn>()
            .With(x => x.AccountId, accountId)
            .With(x => x.Email, "bob@acme.com"));
    }

    [BobcatSpec(typeof(CaptureLead), Domain = "Leads")]
    public async Task a_company_whose_lead_was_disqualified_starts_over_as_a_new_lead()
    {
        var leadId = Guid.CreateVersion7();
        await GivenEvents<Lead>(leadId,
            AcmeWroteIn(leadId),
            new LeadDisqualified(leadId, "Student project", Yesterday.AddHours(2), "jeremy@jasperfx.net"));

        await WhenPosted(Submission("Carol Poe", "carol@acme.com", "We now have budget."), "/api/leads");

        ThenEvents(Specify<LeadCaptured>()
            .With(x => x.CompanyDomain, "acme.com")
            .With(x => x.Contact, new LeadContact("Carol Poe", "carol@acme.com", "carol@acme.com")));
    }
}
#endregion
