using JasperFx.Core;
using SagaWorkflows.OrderApproval;
using Wolverine.Persistence.Sagas;

namespace SagaWorkflows.Specs.OrderApproval;

// Dapr's order processing workflow: a big order waits for a manager's answer or two days, whichever comes first. The
// manager's answer is a signal a person sends, so it can arrive after the order is closed.

[BobcatFeature("A big order waits for a manager")]
[Collection(nameof(DocumentSagaCollection))]
public class approving_an_order(DocumentSagas app) : SagaWorkflowSpec(app)
{
    private Guid OrderId { get; } = Guid.NewGuid();

    [BobcatSpec(typeof(PlaceOrder))]
    public async Task a_small_order_needs_no_approval()
    {
        WarehouseHolds(10, "kettle");

        await WhenReceived(new PlaceOrder(OrderId, "kettle", 2, 300m));

        ThenNoMessageSent<RequestApproval>();
        ThenMessageSent(new NotifyCustomer(OrderId, $"Order {OrderId} has completed!"));
    }

    [BobcatSpec(typeof(ManagerDecision))]
    public async Task an_approval_while_the_order_waits_takes_the_payment()
    {
        WarehouseHolds(10, "forklift");
        await GivenReceived(new PlaceOrder(OrderId, "forklift", 1, 75_000m));

        await WhenReceived(new ManagerDecision(OrderId, Approved: true, "grace"));

        ThenMessageSent(new ProcessPayment(OrderId, "forklift", 1, 75_000m));
        ThenMessageSent(new NotifyCustomer(OrderId, $"Order {OrderId} has completed!"));
    }

    // The timeout is two days out. Letting the time pass runs it, and only it
    [BobcatSpec(typeof(PlaceOrder))]
    public async Task an_order_nobody_approves_is_cancelled_after_two_days()
    {
        WarehouseHolds(10, "forklift");
        await WhenReceived(new PlaceOrder(OrderId, "forklift", 1, 75_000m));
        ThenMessageScheduled<ApprovalTimedOut>();

        await WhenTimePasses(2.Days());

        ThenMessageSent(new NotifyCustomer(OrderId, "Cancelling order because it didn't receive an approval"));
        ThenNoMessageSent<ProcessPayment>();
    }

    // The order is retained after it closes, and a retained, completed saga counts as missing. [DiscardIfSagaMissing]
    // on Handle(ManagerDecision) logs the late answer and drops it, instead of an UnknownSagaException
    [BobcatSpec(typeof(ManagerDecision))]
    public async Task an_answer_after_the_order_timed_out_is_dropped()
    {
        WarehouseHolds(10, "forklift");
        await WhenReceived(new PlaceOrder(OrderId, "forklift", 1, 75_000m));
        await WhenTimePasses(2.Days());

        await WhenReceived(new ManagerDecision(OrderId, Approved: true, "grace"));

        ThenHandled<ManagerDecision>();
        ThenNotDeadLettered<ManagerDecision>();
        ThenNoMessageSent<ProcessPayment>();
    }

    // [RecordSagaHistory]: a document saga records a row for every message it handles, read the same way as an
    // event-sourced saga's stream, so CritterWatch shows one timeline for both
    [BobcatSpec(typeof(ManagerDecision))]
    public async Task the_order_records_every_message_it_handled()
    {
        WarehouseHolds(10, "forklift");
        await GivenReceived(new PlaceOrder(OrderId, "forklift", 1, 75_000m));
        await GivenReceived(new ManagerDecision(OrderId, Approved: true, "grace"));

        await WhenTheSagaHistoryIsRead<Order>(OrderId);

        ThenTheSagaHistoryIs(typeof(PlaceOrder), typeof(InventoryChecked), typeof(ManagerDecision),
            typeof(PaymentProcessed), typeof(InventoryUpdated));
        ThenTheSagaHistoryOutcomesAre(SagaHistoryOutcome.Started, SagaHistoryOutcome.Continued,
            SagaHistoryOutcome.Continued, SagaHistoryOutcome.Continued, SagaHistoryOutcome.Completed);
        await ThenTheSagaSupportsHistory<Order>();
    }
}
