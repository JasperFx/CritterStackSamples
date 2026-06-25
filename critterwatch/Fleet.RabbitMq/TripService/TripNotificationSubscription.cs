using JasperFx.Events;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Marten.Subscriptions;

namespace TripService;

/// <summary>
/// Minimal no-op Marten subscription. It exists so the service exposes a shard whose type is
/// <c>Subscription</c> (rather than a projection) — which is what lights up the <b>Rewind</b> action in
/// the CritterWatch UI (gated on <c>isSubscription</c>). It does no work beyond observing each event page,
/// so it advances its own progression like any real subscription and a rewind re-runs it from zero.
/// </summary>
public class TripNotificationSubscription : SubscriptionBase
{
    public override Task<IChangeListener> ProcessEventsAsync(
        EventRange page,
        ISubscriptionController controller,
        IDocumentOperations operations,
        CancellationToken cancellationToken)
    {
        // No durable side effects — purely here to surface a Subscription-typed shard.
        return Task.FromResult(NullChangeListener.Instance);
    }
}
