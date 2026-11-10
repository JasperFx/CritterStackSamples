using Alba;
using Basket;
using Catalog;
using Discount;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Ordering;
using Shouldly;

namespace Tests;

// The original tests, on xUnit v3. Five of them (basket checkout and delete, three Ordering tests) now live
// in BasketAndOrderingSpecs as Bobcat specifications; the rest stay plain CRUD tests the specs already exemplify.
public class EcommerceTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    #region Catalog

    [Fact]
    public async Task Can_create_product()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct(
                "Widget",
                ["Electronics"],
                "A fine widget",
                "widget.png",
                29.99m
            )).ToUrl("/products");

            x.StatusCodeShouldBe(200);
        });

        var product = result.ReadAsJson<Product>();
        product.ShouldNotBeNull();
        product!.Id.ShouldNotBe(Guid.Empty);
        product.Name.ShouldBe("Widget");
        product.Price.ShouldBe(29.99m);
    }

    [Fact]
    public async Task Can_update_product()
    {
        // Create first
        var created = (await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct("Original", ["Cat"], "Desc", "img.png", 10m)).ToUrl("/products");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<Product>()!;

        // Update
        var result = await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateProduct(
                created.Id,
                "Updated",
                ["NewCat"],
                "New desc",
                "new.png",
                20m
            )).ToUrl("/products");

            x.StatusCodeShouldBe(200);
        });

        var updated = result.ReadAsJson<Product>();
        updated.ShouldNotBeNull();
        updated!.Name.ShouldBe("Updated");
        updated.Price.ShouldBe(20m);
    }

    [Fact]
    public async Task Can_delete_product()
    {
        var created = (await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct("ToDelete", ["Cat"], "Desc", "img.png", 5m)).ToUrl("/products");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<Product>()!;

        await _host.Scenario(x =>
        {
            x.Delete.Url($"/products/{created.Id}");
            x.StatusCodeShouldBe(204);
        });

        // Verify deleted
        await _host.Scenario(x =>
        {
            x.Get.Url($"/products/{created.Id}");
            x.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Can_get_products()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct("P1", ["A"], "D1", "i1.png", 1m)).ToUrl("/products");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct("P2", ["B"], "D2", "i2.png", 2m)).ToUrl("/products");
            x.StatusCodeShouldBe(200);
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/products");
            x.StatusCodeShouldBe(200);
        });

        var products = result.ReadAsJson<List<Product>>();
        products.ShouldNotBeNull();
        (products!.Count >= 2).ShouldBeTrue();
    }

    [Fact]
    public async Task Can_get_product_by_id()
    {
        var created = (await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct("FindMe", ["Cat"], "Desc", "img.png", 15m)).ToUrl("/products");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<Product>()!;

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/products/{created.Id}");
            x.StatusCodeShouldBe(200);
        });

        var product = result.ReadAsJson<Product>();
        product.ShouldNotBeNull();
        product!.Name.ShouldBe("FindMe");
    }

    [Fact]
    public async Task Can_get_products_by_category()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateProduct("CatProd", ["UniqueCategory"], "Desc", "img.png", 10m)).ToUrl("/products");
            x.StatusCodeShouldBe(200);
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/products/category/UniqueCategory");
            x.StatusCodeShouldBe(200);
        });

        var products = result.ReadAsJson<List<Product>>();
        products.ShouldNotBeNull();
        products!.ShouldHaveSingleItem();
        products[0].Name.ShouldBe("CatProd");
    }

    #endregion

    #region Basket

    [Fact]
    public async Task Can_store_and_get_basket()
    {
        var cart = new ShoppingCart
        {
            Id = "testuser",
            Items =
            [
                new ShoppingCartItem
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Widget",
                    Quantity = 2,
                    Price = 10m,
                    Color = "Red"
                }
            ]
        };

        await _host.Scenario(x =>
        {
            x.Post.Json(new StoreBasket(cart)).ToUrl("/basket");
            x.StatusCodeShouldBe(200);
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/basket/testuser");
            x.StatusCodeShouldBe(200);
        });

        var retrieved = result.ReadAsJson<ShoppingCart>();
        retrieved.ShouldNotBeNull();
        retrieved!.Id.ShouldBe("testuser");
        retrieved.Items.ShouldHaveSingleItem();
    }

    #endregion

    #region Ordering

    [Fact]
    public async Task Can_get_orders()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateOrder(
                Guid.NewGuid(), "Order-A",
                "A", "B", "a@b.com", "Addr", "US", "TX", "77001",
                "AB", "4333333333333333", "01/30", "789", 1,
                [new OrderItem { ProductId = Guid.NewGuid(), ProductName = "Item", Quantity = 1, Price = 10m }]
            )).ToUrl("/orders");
            x.StatusCodeShouldBe(200);
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/orders");
            x.StatusCodeShouldBe(200);
        });

        var orders = result.ReadAsJson<List<OrderDto>>();
        orders.ShouldNotBeNull();
        orders!.ShouldNotBeEmpty();
    }

    #endregion

    #region Discount

    [Fact]
    public async Task Can_create_coupon()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateCoupon("TestProduct", "Test discount", 25m)).ToUrl("/discounts");
            x.StatusCodeShouldBe(200);
        });

        var coupon = result.ReadAsJson<Coupon>();
        coupon.ShouldNotBeNull();
        coupon!.ProductName.ShouldBe("TestProduct");
        coupon.Amount.ShouldBe(25m);
    }

    [Fact]
    public async Task Can_get_coupon_by_product_name()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateCoupon("UniqueProd", "Unique discount", 50m)).ToUrl("/discounts");
            x.StatusCodeShouldBe(200);
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/discounts/UniqueProd");
            x.StatusCodeShouldBe(200);
        });

        var coupon = result.ReadAsJson<Coupon>();
        coupon.ShouldNotBeNull();
        coupon!.ProductName.ShouldBe("UniqueProd");
    }

    [Fact]
    public async Task Can_update_coupon()
    {
        var created = (await _host.Scenario(x =>
        {
            x.Post.Json(new CreateCoupon("UpdProd", "Original", 10m)).ToUrl("/discounts");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<Coupon>()!;

        var result = await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateCoupon(created.Id, "UpdProd", "Updated", 20m)).ToUrl("/discounts");
            x.StatusCodeShouldBe(200);
        });

        var updated = result.ReadAsJson<Coupon>();
        updated.ShouldNotBeNull();
        updated!.Description.ShouldBe("Updated");
        updated.Amount.ShouldBe(20m);
    }

    [Fact]
    public async Task Can_delete_coupon()
    {
        var created = (await _host.Scenario(x =>
        {
            x.Post.Json(new CreateCoupon("DelProd", "Delete me", 5m)).ToUrl("/discounts");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<Coupon>()!;

        await _host.Scenario(x =>
        {
            x.Delete.Url($"/discounts/{created.Id}");
            x.StatusCodeShouldBe(204);
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/discounts/DelProd");
            x.StatusCodeShouldBe(404);
        });
    }

    #endregion
}
