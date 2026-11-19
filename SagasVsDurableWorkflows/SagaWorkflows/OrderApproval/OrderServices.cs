using System.Collections.Concurrent;

namespace SagaWorkflows.OrderApproval;

public class PaymentFailedException(string message) : Exception(message);

/// <summary>The warehouse, the payment gateway and the customer's inbox, in memory</summary>
public class Storefront
{
    private readonly ConcurrentDictionary<string, int> _stock = new();

    public ConcurrentBag<string> BrokenStockUpdates { get; } = [];
    public ConcurrentQueue<NotifyCustomer> Notifications { get; } = new();
    public ConcurrentQueue<RequestApproval> ApprovalRequests { get; } = new();
    public ConcurrentBag<Guid> Payments { get; } = [];

    public void Stock(string item, int quantity) => _stock[item] = quantity;
    public int StockOf(string item) => _stock.GetValueOrDefault(item);

    public string[] NotificationsFor(Guid orderId)
        => Notifications.Where(x => x.OrderId == orderId).Select(x => x.Text).ToArray();
}

public static class StorefrontHandler
{
    public static InventoryChecked Handle(ReserveInventory command, Storefront store)
        => new(command.OrderId, store.StockOf(command.Item) >= command.Quantity);

    public static void Handle(RequestApproval command, Storefront store) => store.ApprovalRequests.Enqueue(command);

    public static PaymentProcessed Handle(ProcessPayment command, Storefront store)
    {
        store.Payments.Add(command.OrderId);
        return new PaymentProcessed(command.OrderId);
    }

    public static InventoryUpdated Handle(UpdateInventory command, Storefront store)
    {
        if (store.BrokenStockUpdates.Contains(command.Item))
            throw new InvalidOperationException($"Stock for {command.Item} could not be updated");

        store.Stock(command.Item, store.StockOf(command.Item) - command.Quantity);
        return new InventoryUpdated(command.OrderId);
    }

    public static void Handle(NotifyCustomer command, Storefront store) => store.Notifications.Enqueue(command);
}
