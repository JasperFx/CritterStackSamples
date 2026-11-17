using Bobcat;
using Bobcat.Xunit;
using OutboxDemo;

namespace OutboxDemo.Tests;

/// <summary>
/// The original RegistrationTests, all four rewritten as Bobcat specifications, plus one new spec for the
/// saga's second message. Submitting a registration cascades the <see cref="Registration" /> saga and
/// <see cref="RegistrationSubmitted" /> through the Marten outbox; the saga handles it and cascades two more
/// messages. The tracked session waits for that whole chain, so a spec can assert the saga's state and
/// every message, which the originals never looked at.
/// </summary>
[BobcatFeature("Registration")]
public class RegistrationSpecs(OutboxHost app) : OutboxSpec(app)
{
    [BobcatSpec(typeof(SubmitRegistration))]
    public async Task submit_registration()
    {
        await WhenPosted(new SubmitRegistration("event-1", "member-1", 100m), "/registration");

        ThenTheResponseStatusIs(204);
        ThenMessageSent(Specify<RegistrationSubmitted>()
            .With(x => x.MemberId, "member-1")
            .With(x => x.EventId, "event-1")
            .With(x => x.Payment, 100m));

        // The saga handled RegistrationSubmitted and cascaded the rest of the workflow
        ThenMessageSent(Specify<SendRegistrationEmail>().With(x => x.MemberId, "member-1"));
        ThenMessageSent(Specify<AddEventAttendee>().With(x => x.MemberId, "member-1").With(x => x.EventId, "event-1"));
        await ThenSingleDocument<Registration>(Specify<Registration>()
            .With(x => x.Payment, 100m)
            .With(x => x.CurrentState, "Submitted"));
    }

    [BobcatSpec(typeof(SubmitRegistration))]
    public async Task registration_is_persisted()
    {
        await WhenPosted(new SubmitRegistration("event-3", "member-3", 200m), "/registration");

        ThenTheResponseStatusIs(204);
        // The endpoint chose the saga's id: the one Registration in the store is the new one
        await ThenSingleDocument<Registration>(Specify<Registration>()
            .With(x => x.MemberId, "member-3")
            .With(x => x.EventId, "event-3"));
    }

    [BobcatSpec(typeof(SubmitRegistration))]
    public async Task duplicate_registration_returns_409()
    {
        await GivenReadModel<Registration>(ARegistration("event-2", "member-2", 50m));

        await WhenPosted(new SubmitRegistration("event-2", "member-2", 75m), "/registration");

        ThenTheResponseStatusIs(409);
        ThenNoMessageSent<RegistrationSubmitted>();
    }

    [BobcatSpec(typeof(SubmitRegistration))]
    public async Task same_member_different_event_is_allowed()
    {
        await GivenReadModel<Registration>(ARegistration("event-4", "member-4", 50m));

        await WhenPosted(new SubmitRegistration("event-5", "member-4", 75m), "/registration");

        ThenTheResponseStatusIs(204);
        ThenMessageSent(Specify<RegistrationSubmitted>().With(x => x.MemberId, "member-4").With(x => x.EventId, "event-5"));
    }

    // New: the saga's other handler, driven by its message
    [BobcatSpec(typeof(RegistrationValidated))]
    public async Task a_validated_registration_moves_the_saga_on()
    {
        var registration = ARegistration("event-6", "member-6", 80m);
        registration.CurrentState = "Submitted";
        await GivenReadModel<Registration>(registration);

        await WhenReceived(new RegistrationValidated(registration.Id));

        await ThenDocument<Registration>(registration.Id, Specify<Registration>().With(x => x.CurrentState, "Validated"));
    }

    private static Registration ARegistration(string eventId, string memberId, decimal payment) => new()
    {
        Id = Guid.NewGuid(),
        RegistrationDate = DateTime.UtcNow,
        EventId = eventId,
        MemberId = memberId,
        Payment = payment
    };
}
