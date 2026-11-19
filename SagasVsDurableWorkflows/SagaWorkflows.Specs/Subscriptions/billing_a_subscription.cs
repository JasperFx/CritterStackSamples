using JasperFx.Core;
using SagaWorkflows.Subscriptions;

namespace SagaWorkflows.Specs.Subscriptions;

// Temporal's subscription: a 14 day trial, then a charge every 30 days. Months of waiting are rows in the durable
// scheduled store; in a spec, the time simply passes.

[BobcatFeature("A subscription bills every period until it ends")]
[Collection(nameof(DocumentSagaCollection))]
public class billing_a_subscription(DocumentSagas app) : SagaWorkflowSpec(app)
{
    private Guid SubscriptionId { get; } = Guid.NewGuid();

    private StartSubscription Subscribe(int periods = 12)
        => new(SubscriptionId, "ada", TrialPeriod: 14.Days(), BillingPeriod: 30.Days(), MaxBillingPeriods: periods,
            InitialCharge: 20m);

    [BobcatSpec(typeof(TrialEnded))]
    public async Task the_first_charge_comes_when_the_trial_ends()
    {
        await WhenReceived(Subscribe());
        ThenMessageSent(new SendEmail(SubscriptionId, "ada", EmailKind.Welcome));

        await WhenTimePasses(13.Days());
        ThenNoMessageSent<ChargeCustomer>();

        await WhenTimePasses(1.Days());
        ThenMessageSent(new ChargeCustomer(SubscriptionId, "ada", 1, 20m));
    }

    [BobcatSpec(typeof(BillingPeriodEnded))]
    public async Task three_periods_are_billed_then_the_subscription_is_over()
    {
        await WhenReceived(Subscribe(periods: 3));

        await WhenTimePasses(120.Days());

        ThenSentExactly(typeof(ChargeCustomer), 3);
        ThenMessageSent(new ChargeCustomer(SubscriptionId, "ada", 3, 20m));
        ThenMessageSent(new SendEmail(SubscriptionId, "ada", EmailKind.SubscriptionOver));
        await ThenTheSagaIsCompleted<Subscription>(SubscriptionId);
    }

    // The new plan's first period starts at once. BillingPeriodEnded is [ReplacesEarlierTimeouts], so the monthly
    // timeout still pending for the old plan never reaches the saga: no stale guard in the saga, no charge at $20
    [BobcatSpec(typeof(ChangePlan))]
    public async Task a_new_plan_replaces_the_pending_billing_period()
    {
        await WhenReceived(Subscribe());
        await WhenTimePasses(14.Days());
        ThenMessageSent(new ChargeCustomer(SubscriptionId, "ada", 1, 20m));

        await WhenReceived(new ChangePlan(SubscriptionId, 30m, 7.Days()));
        await WhenTimePasses(35.Days());

        ThenSentExactly(typeof(ChargeCustomer), 5);
        ThenMessageSent(new ChargeCustomer(SubscriptionId, "ada", 2, 30m));
        ThenMessageSent(new ChargeCustomer(SubscriptionId, "ada", 6, 30m));
    }

    [BobcatSpec(typeof(CancelSubscription))]
    public async Task cancelling_in_the_trial_never_charges()
    {
        await WhenReceived(Subscribe());

        await WhenReceived(new CancelSubscription(SubscriptionId));
        ThenMessageSent(new SendEmail(SubscriptionId, "ada", EmailKind.CancelledDuringTrial));

        await WhenTimePasses(30.Days());
        ThenNoMessageSent<ChargeCustomer>();
    }

    // [SagaMissing(Discard)] on the saga covers every message it handles: a signal after the end is logged and dropped
    [BobcatSpec(typeof(ChangePlan))]
    public async Task a_plan_change_after_the_subscription_ended_is_dropped()
    {
        await GivenReceived(Subscribe());
        await GivenReceived(new CancelSubscription(SubscriptionId));

        await WhenReceived(new ChangePlan(SubscriptionId, 30m, 7.Days()));

        ThenHandled<ChangePlan>();
        ThenNotDeadLettered<ChangePlan>();
        ThenNoMessageScheduled<BillingPeriodEnded>();
    }
}
