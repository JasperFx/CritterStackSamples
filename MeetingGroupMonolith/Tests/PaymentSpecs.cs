using Bobcat;
using Bobcat.Xunit;
using Payments;

namespace Tests;

/// <summary>
/// The Payments module, the one event-sourced module in this monolith, as Bobcat specifications: each
/// sends ONE request and asserts what it appended to the event store and what the Subscription aggregate
/// became, where the original tests could only check the returned id.
/// </summary>
[BobcatFeature("Payments")]
public class PaymentSpecs(MeetingGroupsHost app) : MeetingGroupsSpec(app)
{
    [BobcatSpec(typeof(CreateSubscription))]
    public async Task create_a_subscription()
    {
        var payerId = Guid.NewGuid();

        await WhenPosted(new CreateSubscription(payerId, "Monthly"), "/api/payments/subscriptions");

        ThenTheResponseStatusIs(200);
        ThenEvents(Specify<SubscriptionCreated>().With(x => x.PayerId, payerId).With(x => x.Period, "Monthly"));
        await ThenAggregate<Subscription>(TheStartedStream<Subscription>(), Specify<Subscription>()
            .With(x => x.PayerId, payerId)
            .With(x => x.Period, "Monthly")
            .With(x => x.Status, SubscriptionStatus.Active));
        ThenTheResponseIsTheSubscriptionId(await TheResponse<Guid>());
    }

    [BobcatSpec(typeof(CreateSubscription))]
    public async Task an_unknown_period_is_refused()
    {
        await WhenPosted(new CreateSubscription(Guid.NewGuid(), "Invalid"), "/api/payments/subscriptions");

        ThenTheResponseStatusIs(400);
        ThenNoEvents();
    }

    [Then("the response is the id of the subscription stream")]
    public void ThenTheResponseIsTheSubscriptionId(Guid returned)
        => SpecAssert.Check("subscription id", returned, TheStartedStream<Subscription>());
}
