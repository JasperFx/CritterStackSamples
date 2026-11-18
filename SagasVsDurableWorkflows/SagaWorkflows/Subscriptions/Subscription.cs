using JasperFx;
using Wolverine;

namespace SagaWorkflows.Subscriptions;

// Rebuilt from Temporal's subscription workflow tutorial (temporalio/subscription-workflow-project-template-typescript):
// a free trial, then a charge every billing period up to a maximum number of periods. The customer can cancel at any
// time, the charge can change while it runs (signals), and anyone can ask how far along it is (queries).

public record StartSubscription(
    Guid SubscriptionId,
    string CustomerId,
    TimeSpan TrialPeriod,
    TimeSpan BillingPeriod,
    int MaxBillingPeriods,
    decimal InitialCharge);

// Signals
public record CancelSubscription(Guid SubscriptionId);
public record UpdateBillingCharge(Guid SubscriptionId, decimal Amount);

// Temporal's sleep() and condition(..., timeout). Each carries its own delay, because it comes from the subscription
public record TrialEnded(Guid SubscriptionId, TimeSpan Delay) : TimeoutMessage(Delay);
public record BillingPeriodEnded(Guid SubscriptionId, int Period, TimeSpan Delay) : TimeoutMessage(Delay);

// Activities
public record ChargeCustomer(Guid SubscriptionId, string CustomerId, int Period, decimal Amount);
public record SendEmail(Guid SubscriptionId, string CustomerId, EmailKind Kind);

public enum EmailKind
{
    Welcome,
    CancelledDuringTrial,
    SubscriptionFinished,
    SubscriptionOver
}

// Kept after it finishes, so the queries still answer for a closed subscription
[RetainWhenCompleted(Days = 30)]
public class Subscription : Saga, IRevisioned
{
    public Guid Id { get; set; }
    public int Version { get; set; }

    public string CustomerId { get; set; } = "";
    public TimeSpan BillingPeriod { get; set; }
    public int MaxBillingPeriods { get; set; }

    // Everything Temporal's queries read is a property here
    public bool InTrial { get; set; } = true;
    public int BillingPeriodNumber { get; set; } = 1;
    public decimal BillingPeriodCharge { get; set; }
    public decimal TotalCharged { get; set; }

    public static (Subscription, SendEmail, TrialEnded) Start(StartSubscription command)
        => (new Subscription
            {
                Id = command.SubscriptionId,
                CustomerId = command.CustomerId,
                BillingPeriod = command.BillingPeriod,
                MaxBillingPeriods = command.MaxBillingPeriods,
                BillingPeriodCharge = command.InitialCharge
            },
            new SendEmail(command.SubscriptionId, command.CustomerId, EmailKind.Welcome),
            new TrialEnded(command.SubscriptionId, command.TrialPeriod));

    public OutgoingMessages Handle(TrialEnded _)
    {
        InTrial = false;
        return chargeOrFinish();
    }

    public OutgoingMessages Handle(BillingPeriodEnded ended)
        // Only the timeout for the current period counts
        => ended.Period == BillingPeriodNumber ? chargeOrFinish() : [];

    public SendEmail Handle(CancelSubscription _)
    {
        // Temporal notices a cancellation after the trial only when its sleep() wakes up. A saga hears it at once
        MarkCompleted();
        return new SendEmail(Id, CustomerId, InTrial ? EmailKind.CancelledDuringTrial : EmailKind.SubscriptionFinished);
    }

    public void Handle(UpdateBillingCharge update) => BillingPeriodCharge = update.Amount;

    // Signals for a subscription that already ended
    public static void NotFound(CancelSubscription _) { }
    public static void NotFound(UpdateBillingCharge _) { }

    // The body of Temporal's while (true) loop: one billing period per timeout
    private OutgoingMessages chargeOrFinish()
    {
        if (BillingPeriodNumber > MaxBillingPeriods)
        {
            MarkCompleted();
            return [new SendEmail(Id, CustomerId, EmailKind.SubscriptionOver)];
        }

        var charge = new ChargeCustomer(Id, CustomerId, BillingPeriodNumber, BillingPeriodCharge);
        TotalCharged += BillingPeriodCharge;
        BillingPeriodNumber++;

        return [charge, new BillingPeriodEnded(Id, BillingPeriodNumber, BillingPeriod)];
    }
}
