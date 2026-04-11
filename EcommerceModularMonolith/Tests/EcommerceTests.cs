using Alba;
using Basket;
using Catalog;
using Discount;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Ordering;

namespace Tests;

public class EcommerceTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

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
        Assert.NotNull(product);
        Assert.NotEqual(Guid.Empty, product!.Id);
        Assert.Equal("Widget", product.Name);
        Assert.Equal(29.99m, product.Price);
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
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated!.Name);
        Assert.Equal(20m, updated.Price);
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
            x.StatusCodeShouldBe(200);
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
        Assert.NotNull(products);
        Assert.True(products!.Count >= 2);
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
        Assert.NotNull(product);
        Assert.Equal("FindMe", product!.Name);
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
        Assert.NotNull(products);
        Assert.Single(products!);
        Assert.Equal("CatProd", products[0].Name);
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
        Assert.NotNull(retrieved);
        Assert.Equal("testuser", retrieved!.Id);
        Assert.Single(retrieved.Items);
    }

    [Fact]
    public async Task Can_delete_basket()
    {
        var cart = new ShoppingCart
        {
            Id = "deleteuser",
            Items = [new ShoppingCartItem { ProductId = Guid.NewGuid(), ProductName = "X", Quantity = 1, Price = 5m, Color = "Blue" }]
        };

        await _host.Scenario(x =>
        {
            x.Post.Json(new StoreBasket(cart)).ToUrl("/basket");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Delete.Url("/basket/deleteuser");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Get.Url("/basket/deleteuser");
            x.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Can_checkout_basket()
    {
        // Store a basket first
        var cart = new ShoppingCart
        {
            Id = "checkoutuser",
            Items = [new ShoppingCartItem { ProductId = Guid.NewGuid(), ProductName = "Laptop", Quantity = 1, Price = 999m, Color = "Silver" }]
        };

        await _host.Scenario(x =>
        {
            x.Post.Json(new StoreBasket(cart)).ToUrl("/basket");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Post.Json(new CheckoutBasket(
                "checkoutuser",
                Guid.NewGuid(),
                "John", "Doe", "john@test.com",
                "123 Main St", "US", "CA", "90210",
                "John Doe", "4111111111111111", "12/28", "123", 1
            )).ToUrl("/basket/checkout");

            x.StatusCodeShouldBe(200);
        });

        // Basket should be deleted after checkout
        await _host.Scenario(x =>
        {
            x.Get.Url("/basket/checkoutuser");
            x.StatusCodeShouldBe(404);
        });
    }

    #endregion

    #region Ordering

    [Fact]
    public async Task Can_create_order()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateOrder(
                Guid.NewGuid(),
                "TestOrder-1",
                "Jane", "Smith", "jane@test.com",
                "456 Oak Ave", "US", "NY", "10001",
                "Jane Smith", "4222222222222222", "06/29", "456", 1,
                [new OrderItem { ProductId = Guid.NewGuid(), ProductName = "Gadget", Quantity = 3, Price = 25m }]
            )).ToUrl("/orders");

            x.StatusCodeShouldBe(200);
        });

        var order = result.ReadAsJson<Order>();
        Assert.NotNull(order);
        Assert.Equal("TestOrder-1", order!.OrderName);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

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
        Assert.NotNull(orders);
        Assert.NotEmpty(orders!);
    }

    [Fact]
    public async Task Can_get_order_by_id()
    {
        var created = (await _host.Scenario(x =>
        {
            x.Post.Json(new CreateOrder(
                Guid.NewGuid(), "FindOrder",
                "F", "L", "f@l.com", "Addr", "US", "WA", "98101",
                "FL", "4444444444444444", "03/30", "000", 1,
                [new OrderItem { ProductId = Guid.NewGuid(), ProductName = "Thing", Quantity = 1, Price = 50m }]
            )).ToUrl("/orders");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<Order>()!;

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/orders/{created.Id}");
            x.StatusCodeShouldBe(200);
        });

        var order = result.ReadAsJson<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal("FindOrder", order!.OrderName);
    }

    [Fact]
    public async Task Can_delete_order()
    {
        var created = (await _host.Scenario(x =>
        {
            x.Post.Json(new CreateOrder(
                Guid.NewGuid(), "DeleteOrder",
                "D", "O", "d@o.com", "Addr", "US", "IL", "60601",
                "DO", "4555555555555555", "09/30", "111", 1,
                [new OrderItem { ProductId = Guid.NewGuid(), ProductName = "Stuff", Quantity = 1, Price = 5m }]
            )).ToUrl("/orders");
            x.StatusCodeShouldBe(200);
        })).ReadAsJson<Order>()!;

        await _host.Scenario(x =>
        {
            x.Delete.Url($"/orders/{created.Id}");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Get.Url($"/orders/{created.Id}");
            x.StatusCodeShouldBe(404);
        });
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
        Assert.NotNull(coupon);
        Assert.Equal("TestProduct", coupon!.ProductName);
        Assert.Equal(25m, coupon.Amount);
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
        Assert.NotNull(coupon);
        Assert.Equal("UniqueProd", coupon!.ProductName);
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
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated!.Description);
        Assert.Equal(20m, updated.Amount);
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
            x.StatusCodeShouldBe(200);
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/discounts/DelProd");
            x.StatusCodeShouldBe(200);
        });

        // Should return null/empty since deleted
        var coupon = result.ReadAsJson<Coupon>();
        Assert.Null(coupon);
    }

    #endregion
}
