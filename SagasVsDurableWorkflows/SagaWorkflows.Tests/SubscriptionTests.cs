using Alba;
using JasperFx.Core;
using SagaWorkflows.Subscriptions;
using Wolverine;
using Wolverine.Tracking;

namespace SagaWorkflows.Tests;

public class SubscriptionTests(AppFixture fixture) : WorkflowContext(fixture)
{
    private Billing Billing => Service<Billing>();

    private static StartSubscription subscription(int maxPeriods = 3)
        => new(Guid.NewGuid(), $"customer-{Guid.NewGuid():N}", TrialPeriod: 14.Days(), BillingPeriod: 30.Days(),
            MaxBillingPeriods: maxPeriods, InitialCharge: 10m);

    private Task<ITrackedSession> send(HttpMethod method, string url, object? body = null)
        => Host.TrackActivity().Timeout(30.Seconds())
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(_ => Host.Scenario(x =>
            {
                if (method == HttpMethod.Put) x.Put.Json(body!).ToUrl(url);
                else if (body == null) x.Post.Url(url);
                else x.Post.Json(body).ToUrl(url);

                x.StatusCodeShouldBe(202);
            })));

    private async Task<BillingInfo> query(Guid id) => (await Host.GetAsJson<BillingInfo>($"/subscriptions/{id}"))!;

    [Fact]
    public async Task bills_every_period_then_finishes()
    {
        var command = subscription(maxPeriods: 3);
        var session = await send(HttpMethod.Post, "/subscriptions", command);
        Billing.EmailsFor(command.SubscriptionId).ShouldBe([EmailKind.Welcome]);

        // The trial ends, then three billing periods pass, then the fourth timeout finds nothing left to bill
        for (var i = 0; i < 4; i++)
        {
            session = await session.PlayScheduledMessagesAsync(30.Seconds());
        }

        Billing.ChargesFor(command.SubscriptionId).ShouldBe([10m, 10m, 10m]);
        Billing.EmailsFor(command.SubscriptionId).ShouldBe([EmailKind.Welcome, EmailKind.SubscriptionOver]);
        (await query(command.SubscriptionId)).ShouldBe(new BillingInfo(command.CustomerId, 4, 10m, 30m, true));
    }

    [Fact]
    public async Task cancelling_in_the_trial_never_charges()
    {
        var command = subscription();
        var session = await send(HttpMethod.Post, "/subscriptions", command);

        await send(HttpMethod.Post, $"/subscriptions/{command.SubscriptionId}/cancel");

        // The trial's timeout still fires, and finds a finished subscription
        await session.PlayScheduledMessagesAsync(30.Seconds());

        Billing.ChargesFor(command.SubscriptionId).ShouldBeEmpty();
        Billing.EmailsFor(command.SubscriptionId).ShouldBe([EmailKind.Welcome, EmailKind.CancelledDuringTrial]);
        (await query(command.SubscriptionId)).Finished.ShouldBeTrue();
    }

    [Fact]
    public async Task a_new_charge_applies_from_the_next_period_and_cancelling_stops_billing()
    {
        var command = subscription(maxPeriods: 12);
        var session = await send(HttpMethod.Post, "/subscriptions", command);
        session = await session.PlayScheduledMessagesAsync(30.Seconds()); // trial over, period 1 charged

        await send(HttpMethod.Put, $"/subscriptions/{command.SubscriptionId}/charge", new NewCharge(25m));
        (await query(command.SubscriptionId)).ShouldBe(new BillingInfo(command.CustomerId, 2, 25m, 10m, false));

        session = await session.PlayScheduledMessagesAsync(30.Seconds()); // period 2 charged at the new price

        await send(HttpMethod.Post, $"/subscriptions/{command.SubscriptionId}/cancel");
        await session.PlayScheduledMessagesAsync(30.Seconds()); // period 3's timeout arrives after the cancel

        Billing.ChargesFor(command.SubscriptionId).ShouldBe([10m, 25m]);
        Billing.EmailsFor(command.SubscriptionId).ShouldBe([EmailKind.Welcome, EmailKind.SubscriptionFinished]);
        (await query(command.SubscriptionId)).TotalCharged.ShouldBe(35m);
    }

    // The loop body is a pure function of the saga's state
    [Fact]
    public void each_period_charges_and_schedules_the_next()
    {
        var saga = new Subscription
        {
            Id = Guid.NewGuid(), CustomerId = "c", BillingPeriod = 30.Days(), MaxBillingPeriods = 2,
            BillingPeriodCharge = 10m
        };

        var first = saga.Handle(new TrialEnded(saga.Id, 14.Days()));

        first.OfType<ChargeCustomer>().Single().Period.ShouldBe(1);
        first.OfType<BillingPeriodEnded>().Single().ShouldBe(new BillingPeriodEnded(saga.Id, 2, 30.Days()));

        // A stale timeout for a period already billed does nothing
        saga.Handle(new BillingPeriodEnded(saga.Id, 1, 30.Days())).ShouldBeEmpty();
    }
}
