namespace PolecatIndexing;

public static class SampleData
{
    private static readonly string[] Regions = ["NA", "EMEA", "APAC", "LATAM"];
    private static readonly string[] Cities = ["Austin", "Chicago", "London", "Berlin", "Oslo", "Tokyo", "Sydney", "Lima"];
    private static readonly string[] Notes =
    [
        "", "", "", "", "", "",
        "Leave at the front desk",
        "Ring the bell twice",
        "Leave with the neighbor at number 12",
        "Fragile, please carry upright",
        "Call ahead before delivery",
        "Back door, the dog is friendly"
    ];

    private static readonly string[] Tags = ["gift", "expedited", "wholesale", "fragile", "backorder", "international"];

    // A fixed seed, so the numbers in the blog post are the numbers you get
    public static IReadOnlyList<Order> Orders(int count)
    {
        var random = new Random(1234);
        var orders = new List<Order>(count);

        for (var i = 1; i <= count; i++)
        {
            // Most orders in a real system are long since delivered. Only about 2% are still open
            var roll = random.Next(100);
            var status = roll < 2 ? OrderStatus.Open
                : roll < 10 ? OrderStatus.Shipped
                : roll < 95 ? OrderStatus.Delivered
                : OrderStatus.Cancelled;

            var lines = Enumerable.Range(0, random.Next(1, 5)).Select(_ => new OrderLine
            {
                Sku = $"SKU-{random.Next(1, 2001):0000}",
                Quantity = random.Next(1, 13),
                UnitPrice = random.Next(5, 500)
            }).ToList();

            orders.Add(new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"ORD-{i:000000}",
                // Mixed casing on purpose, because that is what people type
                CustomerEmail = $"Customer{random.Next(1, 20001)}@Example.com",
                Region = Regions[random.Next(Regions.Length)],
                Status = status,
                PlacedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i * 4),
                ShippingAddress = new Address
                {
                    City = Cities[random.Next(Cities.Length)],
                    PostalCode = random.Next(10000, 99999).ToString()
                },
                Lines = lines,
                Tags = Tags.Where(_ => random.Next(20) == 0).ToArray(),
                DeliveryNotes = Notes[random.Next(Notes.Length)]
            });
        }

        return orders;
    }
}
