using System.Collections.Concurrent;

namespace SagaWorkflows.Subscriptions;

/// <summary>The payment provider and the mail server, in memory</summary>
public class Billing
{
    public ConcurrentQueue<ChargeCustomer> Charges { get; } = new();
    public ConcurrentQueue<SendEmail> Emails { get; } = new();

    public decimal[] ChargesFor(Guid id) => Charges.Where(x => x.SubscriptionId == id).Select(x => x.Amount).ToArray();
    public EmailKind[] EmailsFor(Guid id) => Emails.Where(x => x.SubscriptionId == id).Select(x => x.Kind).ToArray();
}

public static class BillingHandler
{
    public static void Handle(ChargeCustomer command, Billing billing) => billing.Charges.Enqueue(command);
    public static void Handle(SendEmail command, Billing billing) => billing.Emails.Enqueue(command);
}
