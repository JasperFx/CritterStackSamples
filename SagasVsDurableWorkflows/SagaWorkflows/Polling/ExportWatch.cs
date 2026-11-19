using JasperFx;
using JasperFx.Core;
using Wolverine;

namespace SagaWorkflows.Polling;

// Rebuilt from Temporal's polling samples (samples-dotnet/src/Polling) and Durable Functions' monitor pattern: keep
// asking a slow external system whether a job is done, waiting longer between tries, until it is done or the
// deadline passes.

public record WatchExport(Guid ExportWatchId, string JobId, TimeSpan GiveUpAfter);

// The wait between polls changes, so the delay is on the message rather than the type
public record PollExport(Guid ExportWatchId, TimeSpan Delay) : TimeoutMessage(Delay);

// The overall deadline, scheduled once at the start. The evaluation couldn't test this: playing every scheduled message
// at once fired the deadline together with the first poll. Letting time pass (WhenTimePasses in the specs) can
public record ExportDeadline(Guid ExportWatchId, TimeSpan Delay) : TimeoutMessage(Delay);

public record ExportReady(Guid ExportWatchId, string JobId, Uri Download);
public record ExportAbandoned(Guid ExportWatchId, string JobId, int Polls, string Reason);

public class ExportWatch : CompensatingSaga, IRevisioned
{
    public static readonly TimeSpan FirstWait = 30.Seconds();
    public static readonly TimeSpan LongestWait = 10.Minutes();

    public Guid Id { get; set; }
    public string JobId { get; set; } = "";
    public int Polls { get; set; }
    public TimeSpan Wait { get; set; }

    public static (ExportWatch, PollExport, ExportDeadline) Start(WatchExport command)
        => (new ExportWatch { Id = command.ExportWatchId, JobId = command.JobId, Wait = FirstWait },
            new PollExport(command.ExportWatchId, FirstWait),
            new ExportDeadline(command.ExportWatchId, command.GiveUpAfter));

    // The loop body. Asking the external system is the activity, done right here; if the call throws, the usual
    // retries apply to this message
    public async Task<object> Handle(PollExport poll, IReportingApi api, CancellationToken cancellation)
    {
        Polls++;
        var status = await api.GetExportStatusAsync(JobId, cancellation);

        if (status.Download is not null)
        {
            MarkCompleted();
            return new ExportReady(Id, JobId, status.Download);
        }

        // Back off, up to a ceiling
        Wait = TimeSpan.FromTicks(Math.Min(Wait.Ticks * 2, LongestWait.Ticks));
        return new PollExport(Id, Wait);
    }

    public ExportAbandoned Handle(ExportDeadline deadline)
    {
        MarkCompleted();
        return new ExportAbandoned(Id, JobId, Polls, "The export wasn't ready in time");
    }

    // This saga's own handler failed through all of its retries: the reporting system is down. A Compensate method for
    // the saga's own message takes the live exception. The PollExport is still dead lettered, tagged "compensated"
    public ExportAbandoned Compensate(PollExport failed, ReportingUnavailableException ex)
    {
        MarkCompleted();
        return new ExportAbandoned(Id, JobId, Polls, ex.Message);
    }
}
