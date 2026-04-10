using Marten;
using Wolverine.Http;
using Wolverine.Persistence;

namespace Catalog;

public static class GetProductsEndpoint
{
    [WolverineGet("/products")]
    public static Task<IReadOnlyList<Product>> Get(IQuerySession session)
        => session.Query<Product>().ToListAsync();
}

public static class GetProductByIdEndpoint
{
    [WolverineGet("/products/{id}")]
    public static Product? Get(Guid id, [Entity] Product? product) => product;
}

public static class GetProductByCategoryEndpoint
{
    [WolverineGet("/products/category/{category}")]
    public static Task<IReadOnlyList<Product>> Get(string category, IQuerySession session)
        => session.Query<Product>()
            .Where(p => p.Category.Contains(category))
            .ToListAsync();
}
