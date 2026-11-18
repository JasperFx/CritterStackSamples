using JasperFx;
using JasperFx.Core;
using Wolverine;

namespace SagaWorkflows.OrderApproval;

// Rebuilt from Dapr's .NET workflow sample (dapr/dotnet-sdk, examples/Workflow/WorkflowConsoleApp,
// OrderProcessingWorkflow): reserve stock, ask a manager to approve a big order and wait for the answer or a timeout,
// take the payment, then update the stock. The same "human in the loop with a timeout" shape is Durable Functions'
// human interaction pattern and Temporal's expense sample.

public record PlaceOrder(Guid OrderId, string Item, int Quantity, decimal TotalCost);

public record ReserveInventory(Guid OrderId, string Item, int Quantity);
public record InventoryChecked(Guid OrderId, bool Success);

public record RequestApproval(Guid OrderId, decimal TotalCost);

// The "external event" / signal: a person answered. Comes in over HTTP (OrderEndpoints.cs)
public record ManagerDecision(Guid OrderId, bool Approved, string Manager);

// Nobody answered in time. The delay is part of the message type
public record ApprovalTimedOut(Guid OrderId) : TimeoutMessage(2.Days());

public record ProcessPayment(Guid OrderId, string Item, int Quantity, decimal TotalCost);
public record PaymentProcessed(Guid OrderId);

public record UpdateInventory(Guid OrderId, string Item, int Quantity);
public record InventoryUpdated(Guid OrderId);

public record NotifyCustomer(Guid OrderId, string Text);

// Kept after it finishes so GET /orders/{id} can still answer, as a workflow tool can for a closed workflow
[RetainWhenCompleted(Days = 7)]
public class Order : CompensatingSaga, IRevisioned
{
    public const decimal ApprovalThreshold = 50_000m;

    public Guid Id { get; set; }
    public int Version { get; set; }

    public string Item { get; set; } = "";
    public int Quantity { get; set; }
    public decimal TotalCost { get; set; }

    // Dapr's SetCustomStatus(). Here it is just saga state, readable from GET /orders/{id}
    public string Stage { get; set; } = "Received";
    public bool? Processed { get; set; }

    public static (Order, NotifyCustomer, ReserveInventory) Start(PlaceOrder command)
        => (new Order
            {
                Id = command.OrderId,
                Item = command.Item,
                Quantity = command.Quantity,
                TotalCost = command.TotalCost
            },
            new NotifyCustomer(command.OrderId,
                $"Received order {command.OrderId} for {command.Quantity} {command.Item} at {command.TotalCost:C}"),
            new ReserveInventory(command.OrderId, command.Item, command.Quantity));

    public OutgoingMessages Handle(InventoryChecked result)
    {
        if (!result.Success) return finish(false, $"Insufficient inventory for {Item}");

        if (TotalCost <= ApprovalThreshold) return takePayment();

        Stage = "Waiting for approval";
        return [new RequestApproval(Id, TotalCost), new ApprovalTimedOut(Id)];
    }

    public OutgoingMessages Handle(ManagerDecision decision)
    {
        // An answer that comes after the timeout, or a second answer, changes nothing
        if (Stage != "Waiting for approval") return [];

        return decision.Approved ? takePayment() : finish(false, "Order was rejected by approver");
    }

    // A manager answering after the order already finished. A TimeoutMessage needs no such method
    public static void NotFound(ManagerDecision decision) { }

    public OutgoingMessages Handle(ApprovalTimedOut _)
        => Stage == "Waiting for approval"
            ? finish(false, "Cancelling order because it didn't receive an approval")
            : [];

    public UpdateInventory Handle(PaymentProcessed _)
    {
        Stage = "Updating inventory";
        return new UpdateInventory(Id, Item, Quantity);
    }

    public OutgoingMessages Handle(InventoryUpdated _) => finish(true, $"Order {Id} has completed!");

    // Dapr catches WorkflowTaskFailedException around this step; this is the same thing, after the step's retries
    public OutgoingMessages Compensate(UpdateInventory failed, ExceptionInfo ex)
        => finish(false, $"Order {Id} Failed! Details: {ex.Message}");

    private OutgoingMessages takePayment()
    {
        Stage = "Processing payment";
        return [new ProcessPayment(Id, Item, Quantity, TotalCost)];
    }

    private OutgoingMessages finish(bool processed, string text)
    {
        Processed = processed;
        Stage = processed ? "Completed" : "Failed";
        MarkCompleted();
        return [new NotifyCustomer(Id, text)];
    }
}
