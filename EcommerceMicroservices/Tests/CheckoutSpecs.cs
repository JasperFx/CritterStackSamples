using Basket;
using Bobcat;
using Bobcat.Xunit;
using Ordering;
using Shared;

namespace Tests;

/// <summary>
/// New: the checkout hand-off between Basket and Ordering, which the original tests never covered
/// (they test only Catalog). Basket publishes <see cref="BasketCheckoutEvent" /> to RabbitMQ, stubbed here,
/// so the spec asserts the integration message itself; Ordering's handler is then specified on its own,
/// receiving that message.
/// </summary>
[BobcatFeature("Basket checkout")]
public class BasketCheckoutSpecs : EcommerceSpec
{
    public BasketCheckoutSpecs(BasketHost app) : base(app.Host)
    {
        // The RabbitMQ endpoint is a stub, which never hands the message on: do not wait for it to arrive
        IncludeExternalTransports = false;
    }

    [BobcatSpec(typeof(CheckoutBasket))]
    public async Task checking_out_publishes_the_checkout_to_ordering_and_deletes_the_basket()
    {
        var customer = Guid.NewGuid();
        await GivenReadModel<ShoppingCart>(new ShoppingCart
        {
            Id = "alice",
            Items =
            [
                new ShoppingCartItem { ProductName = "IPhone X", Quantity = 2, Price = 950m, Color = "Black", ProductId = Guid.NewGuid() },
                new ShoppingCartItem { ProductName = "HTC U11+", Quantity = 1, Price = 380m, Color = "Red", ProductId = Guid.NewGuid() }
            ]
        });

        await WhenPosted(Checkout("alice", customer), "/basket/checkout");

        ThenTheResponseStatusIs(200);
        ThenMessageSent(Specify<BasketCheckoutEvent>()
            .With(x => x.UserName, "alice")
            .With(x => x.CustomerId, customer)
            .With(x => x.TotalPrice, 2280m));
        await ThenNoReadModel<ShoppingCart>("alice");
    }

    [BobcatSpec(typeof(CheckoutBasket))]
    public async Task checking_out_without_a_basket_is_refused_and_publishes_nothing()
    {
        await WhenPosted(Checkout("nobody", Guid.NewGuid()), "/basket/checkout");

        ThenTheResponseStatusIs(404);
        ThenNoMessageSent<BasketCheckoutEvent>();
    }

    internal static CheckoutBasket Checkout(string userName, Guid customer)
        => new(userName, customer, "Alice", "Smith", "alice@test.com", "1 Main St", "US", "TX", "75001",
            "Alice Smith", "4111111111111111", "12/30", "123", 1);
}

[BobcatFeature("Ordering")]
public class OrderingSpecs(OrderingHost app) : EcommerceSpec(app.Host)
{
    [BobcatSpec(typeof(BasketCheckoutEvent))]
    public async Task a_basket_checkout_becomes_a_pending_order()
    {
        var customer = Guid.NewGuid();

        await WhenReceived(new BasketCheckoutEvent("alice", customer, 2280m, "Alice", "Smith", "alice@test.com",
            "1 Main St", "US", "TX", "75001", "Alice Smith", "4111111111111111", "12/30", "123", 1));

        // The handler chose the order's id, and the store was empty: the one Order there is the new one
        await ThenSingleDocument<Order>(Specify<Order>()
            .With(x => x.CustomerId, customer)
            .With(x => x.FirstName, "Alice")
            .With(x => x.LastName, "Smith")
            .With(x => x.Status, OrderStatus.Pending));
    }
}
