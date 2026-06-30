using System.Collections.Concurrent;
using System.Text;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CritterWatch.Samples.Testing;

/// <summary>
/// Captures every Aspire resource's console output so the test battery can surface it when an assertion
/// fails. Aspire routes child-resource (container + project) stdout/stderr to the dashboard, NOT to the
/// AppHost's own stdout, so a plain <c>dotnet test</c> run can't otherwise see why a monitored service
/// failed to register — turning an opaque "Registered: [(none)]" into a readable transcript.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why an <see cref="ILoggerProvider"/> and not <c>ResourceLoggerService.WatchAsync</c>:</b> under the
/// <c>Aspire.Hosting.Testing</c> host the dashboard is off, and with no dashboard consuming the DCP console
/// stream <c>ResourceLoggerService</c> stays empty (both <c>WatchAsync</c> and <c>GetAllAsync</c> return
/// nothing). However the host's <c>EnableResourceLogging</c> path re-emits each resource's console output
/// through the standard .NET logging pipeline under the category <c>AppHost.Resources.&lt;resourceName&gt;</c>.
/// Registering this provider on the AppHost's logging therefore captures the full boot output of every
/// container and project, regardless of dashboard state.
/// </para>
/// <para>
/// Capture is best-effort and bounded: each resource keeps only its most recent
/// <see cref="MaxLinesPerResource"/> lines (oldest dropped), so a chatty container can't exhaust memory.
/// </para>
/// </remarks>
internal sealed class ResourceLogCapture : ILoggerProvider
{
    /// <summary>The logging category prefix the AppHost uses when re-emitting a resource's console output.</summary>
    public const string ResourceCategoryPrefix = "AppHost.Resources.";

    /// <summary>Max lines retained per resource — keep the tail, where a startup failure lives.</summary>
    private const int MaxLinesPerResource = 2000;

    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _logs =
        new(StringComparer.OrdinalIgnoreCase);

    private string[] _resourceOrder = [];

    /// <summary>
    /// Records the resource set (for a stable, complete dump order). Call once after
    /// <c>builder.BuildAsync()</c>; capture itself happens via <see cref="CreateLogger"/> as the host logs.
    /// </summary>
    public void Start(DistributedApplication app)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        _resourceOrder = model.Resources
            .Select(r => r.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (categoryName.StartsWith(ResourceCategoryPrefix, StringComparison.Ordinal))
        {
            var resourceName = categoryName[ResourceCategoryPrefix.Length..];
            return new ResourceLogger(this, resourceName);
        }

        // Everything else (the orchestrator's own chatty notification/DCP categories) is noise for our
        // purposes — drop it so the dump stays focused on actual resource console output.
        return NullLogger.Instance;
    }

    private void Append(string resourceName, string line)
    {
        var buffer = _logs.GetOrAdd(resourceName, _ => new ConcurrentQueue<string>());
        buffer.Enqueue(line);
        while (buffer.Count > MaxLinesPerResource)
        {
            buffer.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Renders the captured tail for the named resources (or every known resource when none are named) as a
    /// single block suitable for appending to a failing assertion's message. Resources with no captured
    /// output are listed explicitly so their silence is itself visible.
    /// </summary>
    public string Dump(params string[] resourceNames)
    {
        var names = resourceNames.Length > 0
            ? resourceNames
            : (_resourceOrder.Length > 0
                ? _resourceOrder
                : _logs.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray());

        var sb = new StringBuilder();
        sb.AppendLine("================ Aspire resource logs (tail) ================");
        foreach (var name in names)
        {
            sb.AppendLine($"---- {name} ----");
            if (_logs.TryGetValue(name, out var buffer) && !buffer.IsEmpty)
            {
                foreach (var line in buffer)
                {
                    sb.AppendLine(line);
                }
            }
            else
            {
                sb.AppendLine("(no output captured)");
            }
            sb.AppendLine();
        }
        sb.AppendLine("============================================================");
        return sb.ToString();
    }

    public void Dispose()
    {
        // Buffers are plain in-memory queues; nothing to release. The DI container disposes this provider on
        // host teardown, which is harmless.
    }

    private sealed class ResourceLogger(ResourceLogCapture owner, string resourceName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (logLevel >= LogLevel.Warning)
            {
                message = $"[{logLevel}] {message}";
            }
            if (exception is not null)
            {
                message = $"{message} :: {exception.GetType().Name}: {exception.Message}";
            }
            owner.Append(resourceName, message);
        }
    }

    /// <summary>Minimal no-op logger for non-resource categories.</summary>
    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }
}
