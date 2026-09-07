namespace InventoryService;

/// <summary>The host application's own document — the thing the console must not touch.</summary>
public class Product
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int OnHand { get; set; }
}

/// <summary>Receive stock for a product.</summary>
public record ReceiveStock(string ProductId, string Name, int Quantity);

public static class ReceiveStockHandler
{
    /// <summary>
    /// An ordinary Wolverine handler on the HOST's own store.
    /// </summary>
    /// <remarks>
    /// ⚠️ This handler is the point of the sample as much as the console is: it must keep running
    /// against the host's own Fisher store while the embedded console runs beside it. If embedding a
    /// console changed where a host's own handlers write, embedded mode would be unusable — so
    /// <c>InventoryIsolationTests</c> asserts this document lands in the host's file and nowhere else.
    /// </remarks>
    public static async Task HandleAsync(ReceiveStock command, IDocumentSession session)
    {
        var product = await session.LoadAsync<Product>(command.ProductId)
                      ?? new Product { Id = command.ProductId, Name = command.Name };

        product.OnHand += command.Quantity;
        session.Store(product);
        await session.SaveChangesAsync();
    }
}
