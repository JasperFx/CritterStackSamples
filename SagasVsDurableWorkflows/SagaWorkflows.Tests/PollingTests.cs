using JasperFx.Core;
using SagaWorkflows.Polling;
using Wolverine.Tracking;

namespace SagaWorkflows.Tests;

public class PollingTests(AppFixture fixture) : WorkflowContext(fixture)
{
    private FakeReportingApi Api => Service<FakeReportingApi>();

    [Fact]
    public async Task polls_with_a_growing_wait_until_the_export_is_ready()
    {
        var job = $"job-{Guid.NewGuid():N}";
        Api.NotReadyFor[job] = 2;
        var id = Guid.NewGuid();

        var session = await Run(new WatchExport(id, job, GiveUpAfter: 1.Hours()));
        session.Sent.SingleMessage<PollExport>().Delay.ShouldBe(30.Seconds());

        session = await session.PlayScheduledMessagesAsync(advanceBy: 30.Seconds());
        session.Sent.SingleMessage<PollExport>().Delay.ShouldBe(1.Minutes());

        session = await session.PlayScheduledMessagesAsync(advanceBy: 1.Minutes());
        session.Sent.SingleMessage<PollExport>().Delay.ShouldBe(2.Minutes());

        session = await session.PlayScheduledMessagesAsync(advanceBy: 2.Minutes());
        session.Executed.SingleMessage<ExportReady>().Download.ToString().ShouldEndWith($"{job}.csv");
        (await LoadSaga<ExportWatch>(id)).ShouldBeNull();
    }

    [Fact]
    public async Task gives_up_at_the_deadline()
    {
        var job = $"job-{Guid.NewGuid():N}";
        Api.NotReadyFor[job] = 100;
        var id = Guid.NewGuid();

        var session = await Run(new WatchExport(id, job, GiveUpAfter: 1.Hours()));

        // 59 minutes: polls at 0:30, 1:30, 3:30, 7:30, 15:30, 25:30, 35:30, 45:30 and 55:30, and no deadline yet
        session = await session.PlayScheduledMessagesAsync(advanceBy: 59.Minutes());
        session.Executed.MessagesOf<PollExport>().Count().ShouldBe(9);
        session.Executed.MessagesOf<ExportAbandoned>().ShouldBeEmpty();

        // The hour is up
        session = await session.PlayScheduledMessagesAsync(advanceBy: 1.Minutes());
        session.Executed.SingleMessage<ExportAbandoned>().Polls.ShouldBe(9);
        (await LoadSaga<ExportWatch>(id)).ShouldBeNull();
    }
}
