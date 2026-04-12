using Alba;
using Catalog;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Tests;

public class CatalogTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

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
    public async Task create_product_returns_product()
    {
        var product = await CreateProduct("Test Phone", ["Smart Phone"], "A test phone", 599.99m);

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal("Test Phone", product.Name);
        Assert.Contains("Smart Phone", product.Category);
        Assert.Equal("A test phone", product.Description);
        Assert.Equal(599.99m, product.Price);
    }

    [Fact]
    public async Task create_product_rejects_empty_name()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct("", ["Phone"], "desc", "img.png", 100m)).ToUrl("/products");
            x.StatusCodeShouldBe(400);
        });
    }

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
        Assert.True(products.Count >= 2);
    }

    [Fact]
    public async Task get_product_by_id_returns_product()
    {
        var created = await CreateProduct("ById Product", ["Cat"], "Desc", 150m);

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/products/{created.Id}");
            x.StatusCodeShouldBeOk();
        });

        var product = result.ReadAsJson<Product>()!;
        Assert.Equal(created.Id, product.Id);
        Assert.Equal("ById Product", product.Name);
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
        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains("Smart Phone", p.Category));
    }

    #endregion

    #region Update Product

    [Fact]
    public async Task update_product_modifies_fields()
    {
        var created = await CreateProduct("Original", ["Cat"], "Original desc", 100m);

        var result = await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateProduct(created.Id, "Updated", ["New Cat"], "Updated desc", "updated.png", 250m)).ToUrl("/products");
            x.StatusCodeShouldBeOk();
        });

        var updated = result.ReadAsJson<Product>()!;
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Updated", updated.Name);
        Assert.Contains("New Cat", updated.Category);
        Assert.Equal("Updated desc", updated.Description);
        Assert.Equal(250m, updated.Price);
    }

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

    [Fact]
    public async Task update_nonexistent_product_returns_404()
    {
        await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateProduct(Guid.NewGuid(), "Name", ["Cat"], "Desc", "img.png", 100m)).ToUrl("/products");
            x.StatusCodeShouldBe(404);
        });
    }

    #endregion

    #region Delete Product

    [Fact]
    public async Task delete_product_removes_it()
    {
        var created = await CreateProduct("ToDelete", ["Cat"], "Desc", 100m);

        await _host.Scenario(x =>
        {
            x.Delete.Url($"/products/{created.Id}");
            x.StatusCodeShouldBe(204);
        });

        // Verify it's gone
        await _host.Scenario(x =>
        {
            x.Get.Url($"/products/{created.Id}");
            x.StatusCodeShouldBe(404);
        });
    }

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
