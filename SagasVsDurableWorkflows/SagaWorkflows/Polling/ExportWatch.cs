using JasperFx;
using JasperFx.Core;
using Wolverine;

namespace SagaWorkflows.Polling;

// Rebuilt from Temporal's polling samples (samples-dotnet/src/Polling) and Durable Functions' monitor pattern: keep
// asking a slow external system whether a job is done, waiting longer between tries, until it is done or we give up.

public record WatchExport(Guid ExportWatchId, string JobId, int MaxPolls);

// The wait between polls changes, so the delay is on the message rather than the type
public record PollExport(Guid ExportWatchId, TimeSpan Delay) : TimeoutMessage(Delay);

public record ExportReady(Guid ExportWatchId, string JobId, Uri Download);
public record ExportAbandoned(Guid ExportWatchId, string JobId, int Polls);

public class ExportWatch : Saga, IRevisioned
{
    public static readonly TimeSpan FirstWait = 30.Seconds();
    public static readonly TimeSpan LongestWait = 10.Minutes();

    public Guid Id { get; set; }
    public int Version { get; set; }

    public string JobId { get; set; } = "";
    public int MaxPolls { get; set; }
    public int Polls { get; set; }
    public TimeSpan Wait { get; set; }

    public static (ExportWatch, PollExport) Start(WatchExport command)
        => (new ExportWatch { Id = command.ExportWatchId, JobId = command.JobId, MaxPolls = command.MaxPolls, Wait = FirstWait },
            new PollExport(command.ExportWatchId, FirstWait));

    // The loop body. Asking the external system is the activity, done right here; if the call throws, the usual
    // retries apply to this message
    public async Task<object> Handle(PollExport _, IReportingApi api, CancellationToken cancellation)
    {
        Polls++;
        var status = await api.GetExportStatusAsync(JobId, cancellation);

        if (status.Download is not null)
        {
            MarkCompleted();
            return new ExportReady(Id, JobId, status.Download);
        }

        if (Polls >= MaxPolls)
        {
            MarkCompleted();
            return new ExportAbandoned(Id, JobId, Polls);
        }

        // Back off, up to a ceiling
        Wait = TimeSpan.FromTicks(Math.Min(Wait.Ticks * 2, LongestWait.Ticks));
        return new PollExport(Id, Wait);
    }
}
