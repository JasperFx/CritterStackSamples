using Bobcat;
using Bobcat.Xunit;
using Catalog;

namespace Tests;

/// <summary>
/// Six of the original Catalog tests, rewritten as Bobcat specifications. Each arranges the product it
/// needs as a document instead of creating it through an earlier request, sends ONE request, and asserts
/// the response and what the store now holds. The other eight are still in <see cref="CatalogTests" />.
/// </summary>
[BobcatFeature("Catalog")]
public class CatalogSpecs(CatalogHost app) : EcommerceSpec(app.Host)
{
    [BobcatSpec(typeof(CreateProduct))]
    public async Task create_product_stores_the_product()
    {
        await WhenPosted(new CreateProduct("Test Phone", ["Smart Phone"], "A test phone", "Test Phone.png", 599.99m), "/products");

        ThenTheResponseStatusIs(200);
        // The endpoint chose the id, and the store was empty: the one Product there is the new one
        await ThenSingleDocument<Product>(Specify<Product>()
            .With(x => x.Name, "Test Phone")
            .With(x => x.Category, ["Smart Phone"])
            .With(x => x.Description, "A test phone")
            .With(x => x.Price, 599.99m));
    }

    [BobcatSpec(typeof(CreateProduct))]
    public async Task create_product_rejects_empty_name()
    {
        await WhenPosted(new CreateProduct("", ["Phone"], "desc", "img.png", 100m), "/products");

        ThenTheResponseStatusIs(400);
        await ThenNoReadModel<Product>();
    }

    [BobcatSpec]
    public async Task get_product_by_id_returns_product()
    {
        var id = Guid.NewGuid();
        await GivenReadModel<Product>(new Product { Id = id, Name = "ById Product", Category = ["Cat"], Description = "Desc", Price = 150m });

        await WhenReading<Product>($"/products/{id}");

        ThenTheResponseStatusIs(200);
        ThenMatches(await TheResponse<Product>(), Specify<Product>().With(x => x.Id, id).With(x => x.Name, "ById Product"));
    }

    [BobcatSpec(typeof(UpdateProduct))]
    public async Task update_product_modifies_fields()
    {
        var id = Guid.NewGuid();
        await GivenReadModel<Product>(new Product { Id = id, Name = "Original", Category = ["Cat"], Description = "Original desc", Price = 100m });

        await WhenPut(new UpdateProduct(id, "Updated", ["New Cat"], "Updated desc", "updated.png", 250m), "/products");

        ThenTheResponseStatusIs(200);
        await ThenDocument<Product>(id, Specify<Product>()
            .With(x => x.Name, "Updated")
            .With(x => x.Category, ["New Cat"])
            .With(x => x.Description, "Updated desc")
            .With(x => x.Price, 250m));
    }

    [BobcatSpec(typeof(UpdateProduct))]
    public async Task update_nonexistent_product_returns_404()
    {
        var id = Guid.NewGuid();

        await WhenPut(new UpdateProduct(id, "Name", ["Cat"], "Desc", "img.png", 100m), "/products");

        ThenTheResponseStatusIs(404);
        await ThenNoReadModel<Product>(id);
    }

    [BobcatSpec]
    public async Task delete_product_removes_it()
    {
        var id = Guid.NewGuid();
        await GivenReadModel<Product>(new Product { Id = id, Name = "ToDelete", Category = ["Cat"], Description = "Desc", Price = 100m });

        await WhenDeleted($"/products/{id}");

        ThenTheResponseStatusIs(204);
        await ThenNoReadModel<Product>(id);
    }
}
