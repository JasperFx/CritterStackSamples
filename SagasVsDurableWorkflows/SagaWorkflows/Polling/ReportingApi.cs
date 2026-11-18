using System.Collections.Concurrent;

namespace SagaWorkflows.Polling;

public record ExportStatus(Uri? Download);

public interface IReportingApi
{
    Task<ExportStatus> GetExportStatusAsync(string jobId, CancellationToken cancellation);
}

/// <summary>A reporting system whose exports take a while, in memory</summary>
public class FakeReportingApi : IReportingApi
{
    // How many more times each job answers "not yet"
    public ConcurrentDictionary<string, int> NotReadyFor { get; } = new();

    public ConcurrentQueue<object> Outcomes { get; } = new();

    public Task<ExportStatus> GetExportStatusAsync(string jobId, CancellationToken cancellation)
    {
        var remaining = NotReadyFor.AddOrUpdate(jobId, -1, (_, count) => count - 1);
        return Task.FromResult(new ExportStatus(remaining < 0 ? new Uri($"https://reports.example.com/{jobId}.csv") : null));
    }
}

public static class ExportOutcomeHandler
{
    public static void Handle(ExportReady ready, FakeReportingApi api) => api.Outcomes.Enqueue(ready);
    public static void Handle(ExportAbandoned abandoned, FakeReportingApi api) => api.Outcomes.Enqueue(abandoned);
}
