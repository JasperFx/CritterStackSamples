using Basket;
using Bobcat;
using Bobcat.Xunit;
using Ordering;
using Shared;

namespace Tests;

/// <summary>
/// The Basket tests that cross a module boundary, rewritten as Bobcat specifications. Checking out
/// cascades <see cref="BasketCheckoutEvent" /> to the durable local "basket-checkout" queue, where the
/// Ordering module turns it into an <see cref="Order" />: the spec arranges the basket as a document,
/// sends ONE request, and asserts the integration message and the order it became, which the original
/// test could not see at all.
/// </summary>
[BobcatFeature("Basket")]
public class BasketSpecs(EcommerceHost app) : EcommerceSpec(app)
{
    [BobcatSpec(typeof(CheckoutBasket))]
    public async Task checking_out_a_basket_hands_ordering_a_pending_order()
    {
        var customer = Guid.NewGuid();
        await GivenReadModel<ShoppingCart>(new ShoppingCart
        {
            Id = "checkoutuser",
            Items = [new ShoppingCartItem { ProductId = Guid.NewGuid(), ProductName = "Laptop", Quantity = 1, Price = 999m, Color = "Silver" }]
        });

        await WhenPosted(new CheckoutBasket(
            "checkoutuser", customer,
            "John", "Doe", "john@test.com",
            "123 Main St", "US", "CA", "90210",
            "John Doe", "4111111111111111", "12/28", "123", 1), "/basket/checkout");

        ThenTheResponseStatusIs(200);
        ThenMessageSent(Specify<BasketCheckoutEvent>()
            .With(x => x.UserName, "checkoutuser")
            .With(x => x.CustomerId, customer)
            .With(x => x.TotalPrice, 999m));
        await ThenNoReadModel<ShoppingCart>("checkoutuser");

        // The Ordering module's handler chose the order's id: the one Order in the store is the new one
        await ThenSingleDocument<Order>(Specify<Order>()
            .With(x => x.CustomerId, customer)
            .With(x => x.FirstName, "John")
            .With(x => x.Status, OrderStatus.Pending));
    }

    [BobcatSpec]
    public async Task deleting_a_basket_removes_it()
    {
        await GivenReadModel<ShoppingCart>(new ShoppingCart
        {
            Id = "deleteuser",
            Items = [new ShoppingCartItem { ProductId = Guid.NewGuid(), ProductName = "X", Quantity = 1, Price = 5m, Color = "Blue" }]
        });

        await WhenDeleted("/basket/deleteuser");

        ThenTheResponseStatusIs(204);
        await ThenNoReadModel<ShoppingCart>("deleteuser");
    }
}

/// <summary>Three of the original Ordering tests, as Bobcat specifications.</summary>
[BobcatFeature("Ordering")]
public class OrderingSpecs(EcommerceHost app) : EcommerceSpec(app)
{
    [BobcatSpec(typeof(CreateOrder))]
    public async Task creating_an_order_stores_a_pending_order()
    {
        var customer = Guid.NewGuid();

        await WhenPosted(new CreateOrder(
            customer, "TestOrder-1",
            "Jane", "Smith", "jane@test.com",
            "456 Oak Ave", "US", "NY", "10001",
            "Jane Smith", "4222222222222222", "06/29", "456", 1,
            [new OrderItem { ProductId = Guid.NewGuid(), ProductName = "Gadget", Quantity = 3, Price = 25m }]), "/orders");

        ThenTheResponseStatusIs(200);
        await ThenSingleDocument<Order>(Specify<Order>()
            .With(x => x.CustomerId, customer)
            .With(x => x.OrderName, "TestOrder-1")
            .With(x => x.Status, OrderStatus.Pending)
            .With(x => x.TotalPrice, 75m));
    }

    [BobcatSpec]
    public async Task an_order_is_read_by_its_id()
    {
        var order = AnOrder("FindOrder");
        await GivenReadModel<Order>(order);

        await WhenReading<OrderDto>($"/orders/{order.Id}");

        ThenTheResponseStatusIs(200);
        ThenMatches(await TheResponse<OrderDto>(), Specify<OrderDto>()
            .With(x => x.Id, order.Id)
            .With(x => x.OrderName, "FindOrder")
            .With(x => x.TotalPrice, 50m));
    }

    [BobcatSpec]
    public async Task deleting_an_order_removes_it()
    {
        var order = AnOrder("DeleteOrder");
        await GivenReadModel<Order>(order);

        await WhenDeleted($"/orders/{order.Id}");

        ThenTheResponseStatusIs(204);
        await ThenNoReadModel<Order>(order.Id);
    }

    private static Order AnOrder(string name) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        OrderName = name,
        FirstName = "F",
        LastName = "L",
        Status = OrderStatus.Pending,
        Items = [new OrderItem { ProductId = Guid.NewGuid(), ProductName = "Thing", Quantity = 1, Price = 50m }],
        CreatedAt = DateTimeOffset.UtcNow
    };
}
