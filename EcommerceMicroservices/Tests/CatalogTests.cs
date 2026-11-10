using Alba;
using Catalog;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Tests;

// The original tests, on xUnit v3. Six of them now live in CatalogSpecs as Bobcat specifications;
// these stay plain: validation variants and list reads that the six specs already exemplify.
public class CatalogTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    #region Helpers

    private async Task<Product> CreateProduct(string name, List<string> category, string description, decimal price)
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct(name, category, description, $"{name}.png", price)).ToUrl("/products");
            x.StatusCodeShouldBeOk();
        });
        return result.ReadAsJson<Product>()!;
    }

    #endregion

    #region Create Product

    [Fact]
    public async Task create_product_rejects_empty_category()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct("Phone", [], "desc", "img.png", 100m)).ToUrl("/products");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task create_product_rejects_zero_price()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct("Phone", ["Phone"], "desc", "img.png", 0m)).ToUrl("/products");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Get Products

    [Fact]
    public async Task get_products_returns_list()
    {
        await CreateProduct("Product A", ["Cat1"], "Desc A", 100m);
        await CreateProduct("Product B", ["Cat2"], "Desc B", 200m);

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/products");
            x.StatusCodeShouldBeOk();
        });

        var products = result.ReadAsJson<List<Product>>()!;
        (products.Count >= 2).ShouldBeTrue();
    }

    [Fact]
    public async Task get_product_by_id_returns_404_for_missing()
    {
        await _host.Scenario(x =>
        {
            x.Get.Url($"/products/{Guid.NewGuid()}");
            x.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task get_products_by_category()
    {
        await CreateProduct("Cat Phone", ["Smart Phone"], "Phone desc", 500m);
        await CreateProduct("Cat Laptop", ["Laptop"], "Laptop desc", 1200m);

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/products/category/Smart Phone");
            x.StatusCodeShouldBeOk();
        });

        var products = result.ReadAsJson<List<Product>>()!;
        products.ShouldNotBeEmpty();
        foreach (var p in products)
        {
            p.Category.ShouldContain("Smart Phone");
        }
    }

    #endregion

    #region Update Product

    [Fact]
    public async Task update_product_rejects_empty_name()
    {
        var created = await CreateProduct("ToUpdate", ["Cat"], "Desc", 100m);

        await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateProduct(created.Id, "", ["Cat"], "Desc", "img.png", 100m)).ToUrl("/products");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task update_product_rejects_zero_price()
    {
        var created = await CreateProduct("ToUpdate2", ["Cat"], "Desc", 100m);

        await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateProduct(created.Id, "Name", ["Cat"], "Desc", "img.png", 0m)).ToUrl("/products");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Delete Product

    [Fact]
    public async Task delete_nonexistent_product_returns_404()
    {
        await _host.Scenario(x =>
        {
            x.Delete.Url($"/products/{Guid.NewGuid()}");
            x.StatusCodeShouldBe(404);
        });
    }

    #endregion
}
