using Alba;
using JasperFx.Core;
using SagaWorkflows.OrderApproval;
using Wolverine;
using Wolverine.Tracking;

namespace SagaWorkflows.Tests;

public class OrderApprovalTests(AppFixture fixture) : WorkflowContext(fixture)
{
    private Storefront Store => Service<Storefront>();

    private string stockedItem(int quantity = 10)
    {
        var item = $"item-{Guid.NewGuid():N}";
        Store.Stock(item, quantity);
        return item;
    }

    private Task<ITrackedSession> post(string url, object body)
        => Host.TrackActivity().Timeout(30.Seconds()).DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(_ => Host.Scenario(x =>
            {
                x.Post.Json(body).ToUrl(url);
                x.StatusCodeShouldBe(202);
            })));

    private async Task<OrderStatus> status(Guid orderId)
        => (await Host.GetAsJson<OrderStatus>($"/orders/{orderId}"))!;

    [Fact]
    public async Task a_small_order_needs_no_approval()
    {
        var item = stockedItem();
        var id = Guid.NewGuid();

        await post("/orders", new PlaceOrder(id, item, 2, 300m));

        Store.NotificationsFor(id).Last().ShouldBe($"Order {id} has completed!");
        Store.StockOf(item).ShouldBe(8);
        (await status(id)).ShouldBe(new OrderStatus(id, "Completed", true, true));
    }

    [Fact]
    public async Task a_big_order_waits_for_the_manager_then_carries_on()
    {
        var item = stockedItem();
        var id = Guid.NewGuid();

        await post("/orders", new PlaceOrder(id, item, 1, 75_000m));

        Store.ApprovalRequests.ShouldContain(x => x.OrderId == id);
        (await status(id)).Stage.ShouldBe("Waiting for approval");

        await post($"/orders/{id}/approval", new ApprovalAnswer(true, "Grace"));

        (await status(id)).ShouldBe(new OrderStatus(id, "Completed", true, true));
        Store.Payments.ShouldContain(id);
    }

    [Fact]
    public async Task a_rejected_order_stops_without_payment()
    {
        var item = stockedItem();
        var id = Guid.NewGuid();

        await post("/orders", new PlaceOrder(id, item, 1, 75_000m));
        await post($"/orders/{id}/approval", new ApprovalAnswer(false, "Grace"));

        Store.NotificationsFor(id).Last().ShouldBe("Order was rejected by approver");
        Store.Payments.ShouldNotContain(id);
        (await status(id)).Processed.ShouldBe(false);
    }

    [Fact]
    public async Task an_order_nobody_approves_is_cancelled_by_the_timeout()
    {
        var item = stockedItem();
        var id = Guid.NewGuid();

        var session = await post("/orders", new PlaceOrder(id, item, 1, 75_000m));

        // Two days pass
        await session.PlayScheduledMessagesAsync(30.Seconds());

        Store.NotificationsFor(id).Last().ShouldBe("Cancelling order because it didn't receive an approval");
        (await status(id)).ShouldBe(new OrderStatus(id, "Failed", false, true));

        // A late answer goes to NotFound, and changes nothing
        var late = await post($"/orders/{id}/approval", new ApprovalAnswer(true, "Grace"));
        late.MovedToErrorQueue.Envelopes().ShouldBeEmpty();
        Store.Payments.ShouldNotContain(id);
    }

    [Fact]
    public async Task an_order_without_stock_fails_at_once()
    {
        var item = stockedItem(quantity: 1);
        var id = Guid.NewGuid();

        await post("/orders", new PlaceOrder(id, item, 5, 100m));

        Store.NotificationsFor(id).Last().ShouldBe($"Insufficient inventory for {item}");
        (await status(id)).Processed.ShouldBe(false);
    }

    [Fact]
    public async Task a_stock_update_that_keeps_failing_fails_the_order()
    {
        var item = stockedItem();
        Store.BrokenStockUpdates.Add(item);
        var id = Guid.NewGuid();

        var session = await post("/orders", new PlaceOrder(id, item, 1, 100m));

        session.MovedToErrorQueue.SingleMessage<UpdateInventory>();
        Store.NotificationsFor(id).Last().ShouldBe($"Order {id} Failed! Details: Stock for {item} could not be updated");

        var order = await LoadSaga<Order>(id);
        order!.Status.ShouldBe(SagaStatus.Compensated);
        order.Stage.ShouldBe("Failed");
    }
}
