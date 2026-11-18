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

        var session = await Run(new WatchExport(id, job, MaxPolls: 10));
        session.Sent.SingleMessage<PollExport>().Delay.ShouldBe(30.Seconds());

        session = await session.PlayScheduledMessagesAsync(30.Seconds());
        session.Sent.SingleMessage<PollExport>().Delay.ShouldBe(1.Minutes());

        session = await session.PlayScheduledMessagesAsync(30.Seconds());
        session.Sent.SingleMessage<PollExport>().Delay.ShouldBe(2.Minutes());

        session = await session.PlayScheduledMessagesAsync(30.Seconds());
        session.Executed.SingleMessage<ExportReady>().Download.ToString().ShouldEndWith($"{job}.csv");
        (await LoadSaga<ExportWatch>(id)).ShouldBeNull();
    }

    [Fact]
    public async Task gives_up_after_the_last_poll()
    {
        var job = $"job-{Guid.NewGuid():N}";
        Api.NotReadyFor[job] = 100;
        var id = Guid.NewGuid();

        var session = await Run(new WatchExport(id, job, MaxPolls: 3));
        for (var i = 0; i < 3; i++)
        {
            session = await session.PlayScheduledMessagesAsync(30.Seconds());
        }

        session.Executed.SingleMessage<ExportAbandoned>().Polls.ShouldBe(3);
        session.Sent.MessagesOf<PollExport>().ShouldBeEmpty();
    }
}
