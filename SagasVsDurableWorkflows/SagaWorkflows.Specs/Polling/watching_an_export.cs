using JasperFx.Core;
using SagaWorkflows.Polling;
using Wolverine;

namespace SagaWorkflows.Specs.Polling;

// Temporal's polling sample and Durable Functions' monitor: poll a slow reporting system with a growing wait, under an
// overall deadline of an hour. The deadline is scheduled at the start, so it needs time to pass in order, not every
// scheduled message played at once.

[BobcatFeature("An export is polled until it's ready or the hour is up")]
[Collection(nameof(DocumentSagaCollection))]
public class watching_an_export(DocumentSagas app) : SagaWorkflowSpec(app)
{
    private Guid ExportWatchId { get; } = Guid.NewGuid();

    // Polls at 0:30, 1:30 and 3:30: the wait doubles each time
    [BobcatSpec(typeof(PollExport))]
    public async Task the_export_is_polled_with_a_growing_wait_until_it_is_ready()
    {
        ExportIsReadyAfter("sales-q3", 2);
        await WhenReceived(new WatchExport(ExportWatchId, "sales-q3", GiveUpAfter: 1.Hours()));

        await WhenTimePasses(5.Minutes());

        ThenMessageSent(new ExportReady(ExportWatchId, "sales-q3", new Uri("https://reports.example.com/sales-q3.csv")));
        ThenNotHandled<ExportDeadline>();
        await ThenNoSaga<ExportWatch>(ExportWatchId);
    }

    [BobcatSpec(typeof(ExportDeadline))]
    public async Task the_watch_gives_up_when_the_hour_is_up()
    {
        ExportIsReadyAfter("sales-q3", 100);
        await WhenReceived(new WatchExport(ExportWatchId, "sales-q3", GiveUpAfter: 1.Hours()));

        await WhenTimePasses(59.Minutes());
        ThenNotHandled<ExportDeadline>();

        await WhenTimePasses(1.Minutes());
        ThenMessageSent(new ExportAbandoned(ExportWatchId, "sales-q3", 9, "The export wasn't ready in time"));
        await ThenNoSaga<ExportWatch>(ExportWatchId);
    }

    // The saga's own handler fails through all of its retries. Compensate(PollExport, ReportingUnavailableException)
    // takes the live exception, and the poll is dead lettered with the "compensated" tag. Compensation runs for a
    // message processed from a queue, so the due poll is delivered to its queue here (the DeliveryOptions say "send it
    // now"), as the scheduler does in production, rather than played inline by letting time pass
    [BobcatSpec(typeof(PollExport))]
    public async Task a_reporting_system_that_stays_down_ends_the_watch_through_compensation()
    {
        ReportingIsDown("sales-q3");
        await GivenReceived(new WatchExport(ExportWatchId, "sales-q3", GiveUpAfter: 1.Hours()));

        await WhenReceived(new PollExport(ExportWatchId, TimeSpan.Zero), options: new DeliveryOptions());

        ThenAttempted<PollExport>(3);
        await ThenDeadLettered<PollExport>(SagaWorkflowsHost.ServiceName, tagged: "compensated");
        ThenMessageSent(Specify<ExportAbandoned>().With(x => x.Reason, "The reporting system is unavailable for sales-q3"));
        await ThenNoSaga<ExportWatch>(ExportWatchId);
    }
}
