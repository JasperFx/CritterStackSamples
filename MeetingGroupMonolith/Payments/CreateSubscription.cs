using FluentValidation;
using Marten;
using MeetingGroupMonolith;
using Wolverine.Http;

namespace Payments;

public record CreateSubscription(Guid PayerId, string Period)
{
    public class Validator : AbstractValidator<CreateSubscription>
    {
        public Validator()
        {
            RuleFor(x => x.PayerId).NotEmpty();
            RuleFor(x => x.Period).NotEmpty().Must(p => p is "Monthly" or "HalfYearly" or "Yearly");
        }
    }
}

public static class CreateSubscriptionEndpoint
{
    // Event-sourced: starts a new event stream in Marten's event store.
    // Cascading message notifies the Meetings module of the subscription change.
    [WolverinePost("/api/payments/subscriptions")]
    public static (Guid, SubscriptionExpirationChangedEvent) Post(
        CreateSubscription command,
        IDocumentSession session)
    {
        var subscriptionId = Guid.NewGuid();
        var expirationDate = command.Period switch
        {
            "Monthly" => DateTime.UtcNow.AddMonths(1),
            "HalfYearly" => DateTime.UtcNow.AddMonths(6),
            "Yearly" => DateTime.UtcNow.AddYears(1),
            _ => DateTime.UtcNow.AddMonths(1),
        };

        // Start a new event stream — Marten stores the event and
        // the Subscription snapshot is built via the Apply methods
        session.Events.StartStream<Subscription>(
            subscriptionId,
            new SubscriptionCreated(subscriptionId, command.PayerId, command.Period, expirationDate));

        return (subscriptionId, new SubscriptionExpirationChangedEvent(command.PayerId, expirationDate));
    }
}
