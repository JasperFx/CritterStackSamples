using Marten.Events.Projections;

namespace OrderService;

// =============================================================================================
// An ASYNC multi-stream projection — rolls every customer's orders into one document. Registered Async in
// Program.cs so it runs on Wolverine-distributed projection agents, giving CritterWatch's Projections view
// (and its pause / restart / rebuild actions) a real target to operate on.
// =============================================================================================

public class CustomerOrders
{
    // Identity = the customer name (this projection groups by customer, not by stream).
    public string Id { get; set; } = string.Empty;
    public int OrdersPlaced { get; set; }
}

public class OrdersByCustomerProjection : MultiStreamProjection<CustomerOrders, string>
{
    public OrdersByCustomerProjection()
    {
        // Group every OrderPlaced event by its customer name into one CustomerOrders document.
        Identity<OrderPlaced>(e => e.Customer);
    }

    public void Apply(OrderPlaced _, CustomerOrders view) => view.OrdersPlaced++;
}
