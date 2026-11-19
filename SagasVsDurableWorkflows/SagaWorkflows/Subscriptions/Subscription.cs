using JasperFx;
using Wolverine;

namespace SagaWorkflows.Subscriptions;

// Rebuilt from Temporal's subscription workflow tutorial (temporalio/subscription-workflow-project-template-typescript):
// a free trial, then a charge every billing period up to a maximum number of periods. The customer can cancel at any
// time, the plan can change while it runs (signals), and anyone can ask how far along it is (queries).

public record StartSubscription(
    Guid SubscriptionId,
    string CustomerId,
    TimeSpan TrialPeriod,
    TimeSpan BillingPeriod,
    int MaxBillingPeriods,
    decimal InitialCharge);

// Signals
public record CancelSubscription(Guid SubscriptionId);

// A new price and billing period. After the trial, the new plan's first period starts right away
public record ChangePlan(Guid SubscriptionId, decimal Charge, TimeSpan BillingPeriod);

// Temporal's condition(cancelled, trialPeriod) and sleep(billingPeriod). Each carries its own delay, because it comes
// from the subscription. A later BillingPeriodEnded for the same subscription replaces the one still pending, so a
// plan change can never be followed by a charge for the old period
public record TrialEnded(Guid SubscriptionId, TimeSpan Delay) : TimeoutMessage(Delay);

[ReplacesEarlierTimeouts]
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

// Kept after it finishes, so the queries still answer for a closed subscription. A signal that arrives after it
// finished is logged and dropped, for every message this saga handles: [SagaMissing(Discard)] instead of a NotFound
// method per signal
[RetainWhenCompleted(Days = 30)]
[SagaMissing(SagaMissingBehavior.Discard)]
public class Subscription : Saga, IRevisioned
{
    public Guid Id { get; set; }
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

    // No "is this the timeout for the current period?" guard: a timeout a later one replaced never gets here
    public OutgoingMessages Handle(BillingPeriodEnded _) => chargeOrFinish();

    public OutgoingMessages Handle(ChangePlan change)
    {
        BillingPeriodCharge = change.Charge;
        BillingPeriod = change.BillingPeriod;

        // In the trial, the new plan starts when the trial ends. Afterward its first period starts now, and the
        // BillingPeriodEnded this schedules replaces the one pending for the old plan
        return InTrial ? [] : [new BillingPeriodEnded(Id, BillingPeriodNumber, BillingPeriod)];
    }

    public SendEmail Handle(CancelSubscription _)
    {
        // Temporal notices a cancellation after the trial only when its sleep() wakes up. A saga hears it at once
        MarkCompleted();
        return new SendEmail(Id, CustomerId, InTrial ? EmailKind.CancelledDuringTrial : EmailKind.SubscriptionFinished);
    }

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
