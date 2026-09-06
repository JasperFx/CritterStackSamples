using Fisher;

namespace InventoryService;

/// <summary>A document that belongs to the HOST application, not to CritterWatch.</summary>
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int OnHand { get; set; }
}

/// <summary>An ordinary command handled by an ordinary handler in the host's own assembly.</summary>
public record ReceiveStock(Guid ProductId, string Name, int Quantity);

public static class ReceiveStockHandler
{
    public static async Task Handle(ReceiveStock command, IDocumentSession session)
    {
        var product = await session.LoadAsync<Product>(command.ProductId)
                      ?? new Product { Id = command.ProductId, Name = command.Name };

        product.OnHand += command.Quantity;
        session.Store(product);
        await session.SaveChangesAsync();
    }
}
