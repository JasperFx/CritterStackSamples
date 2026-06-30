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

// MUST be `partial`: this projection dispatches by CONVENTION methods (the Apply below), and JasperFx's
// compile-time source generator (JasperFx.Events.SourceGenerator, shipped inside the Marten NuGet) emits the
// dispatcher into a generated partial of this class. There is NO runtime fallback — a non-partial convention
// projection throws InvalidProjectionException at startup ("No source-generated dispatcher found …"), which
// would crash OrderService before it ever registers with the console. (A self-aggregating Snapshot<T> like
// Order does not need this; a convention-method projection subclass like this one does.)
public partial class OrdersByCustomerProjection : MultiStreamProjection<CustomerOrders, string>
{
    public OrdersByCustomerProjection()
    {
        // Group every OrderPlaced event by its customer name into one CustomerOrders document.
        Identity<OrderPlaced>(e => e.Customer);
    }

    public void Apply(OrderPlaced _, CustomerOrders view) => view.OrdersPlaced++;
}
