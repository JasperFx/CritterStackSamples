using Wolverine.Http;
using Wolverine.Persistence;

namespace SagaWorkflows.Subscriptions;

public record NewPlan(decimal Charge, TimeSpan BillingPeriod);

public record BillingInfo(string CustomerId, int BillingPeriodNumber, decimal BillingPeriodCharge, decimal TotalCharged,
    bool Finished);

public static class SubscriptionEndpoints
{
    [WolverinePost("/subscriptions")]
    public static (AcceptResponse, StartSubscription) Post(StartSubscription command)
        => (new AcceptResponse($"/subscriptions/{command.SubscriptionId}"), command);

    // Temporal's signals
    [WolverinePost("/subscriptions/{subscriptionId}/cancel")]
    public static (AcceptResponse, CancelSubscription) Cancel(Guid subscriptionId)
        => (new AcceptResponse($"/subscriptions/{subscriptionId}"), new CancelSubscription(subscriptionId));

    [WolverinePut("/subscriptions/{subscriptionId}/plan")]
    public static (AcceptResponse, ChangePlan) Plan(Guid subscriptionId, NewPlan plan)
        => (new AcceptResponse($"/subscriptions/{subscriptionId}"),
            new ChangePlan(subscriptionId, plan.Charge, plan.BillingPeriod));

    // Temporal's queries, all at once
    [WolverineGet("/subscriptions/{id}")]
    public static BillingInfo Get([Entity] Subscription subscription)
        => new(subscription.CustomerId, subscription.BillingPeriodNumber, subscription.BillingPeriodCharge,
            subscription.TotalCharged, subscription.IsCompleted());
}
