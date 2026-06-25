namespace OrderService;

// =============================================================================================
// The Orders domain — a tiny event-sourced aggregate so the OrderService has real event-store activity
// for CritterWatch to surface (streams, an async projection, a rebuild target). Past-tense events,
// imperative commands — the standard Critter Stack conventions.
// =============================================================================================

// ---- Events (past tense) ----------------------------------------------------------------------
public record OrderPlaced(string Customer, string[] Items);

public record OrderItemAdded(string Item);

public record OrderShipped(DateTimeOffset ShippedAt);

// ---- The aggregate ----------------------------------------------------------------------------
// Marten's "self-aggregating" style: static Create/Apply methods fold the event stream into this shape.
public class Order
{
    public Guid Id { get; set; }
    public string Customer { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new();
    public bool Shipped { get; set; }

    public static Order Create(OrderPlaced placed) => new()
    {
        Customer = placed.Customer,
        Items = placed.Items.ToList()
    };

    public void Apply(OrderItemAdded added) => Items.Add(added.Item);

    public void Apply(OrderShipped _) => Shipped = true;
}
