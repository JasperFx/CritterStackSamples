namespace MartenIndexing;

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string Region { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset PlacedAt { get; set; }
    public Address ShippingAddress { get; set; } = new();
    public List<OrderLine> Lines { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public string DeliveryNotes { get; set; } = "";
}

public class Address
{
    public string City { get; set; } = "";
    public string PostalCode { get; set; } = "";
}

public class OrderLine
{
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public static class OrderStatus
{
    public const string Open = "Open";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";
}
