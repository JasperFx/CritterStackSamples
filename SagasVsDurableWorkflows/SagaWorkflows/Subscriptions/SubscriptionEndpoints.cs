using Wolverine.Http;
using Wolverine.Persistence;

namespace SagaWorkflows.Subscriptions;

public record NewCharge(decimal Amount);

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

    [WolverinePut("/subscriptions/{subscriptionId}/charge")]
    public static (AcceptResponse, UpdateBillingCharge) Charge(Guid subscriptionId, NewCharge charge)
        => (new AcceptResponse($"/subscriptions/{subscriptionId}"), new UpdateBillingCharge(subscriptionId, charge.Amount));

    // Temporal's queries, all at once
    [WolverineGet("/subscriptions/{id}")]
    public static BillingInfo Get([Entity] Subscription subscription)
        => new(subscription.CustomerId, subscription.BillingPeriodNumber, subscription.BillingPeriodCharge,
            subscription.TotalCharged, subscription.IsCompleted());
}
