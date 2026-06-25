using Incidents.Domain;
using JasperFx.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Runtime;

namespace Incidents.Service;

/// <summary>
/// Keeps a steady backlog of future-dated <see cref="IncidentReminder"/> messages so the CritterWatch
/// Scheduled Messages page always has realistic pending rows. Schedules one every few seconds for delivery
/// 1–10 minutes out. Injects <see cref="IWolverineRuntime"/> (not the scoped <see cref="IMessageBus"/>)
/// per the BackgroundService rule, opening a fresh bus per schedule.
///
/// <para>
/// This fleet's durable store IS the SQL Server queue database, so these scheduled messages land in the
/// durable store CritterWatch's Scheduled panel reads — the panel fully populates (the "richest fleet"
/// property the DB-queue fleets get for free).
/// </para>
/// </summary>
public class ReminderScheduler : IHostedService
{
    private readonly IWolverineRuntime _runtime;
    private readonly ILogger<ReminderScheduler> _logger;
    private CancellationTokenSource? _cts;

    public ReminderScheduler(IWolverineRuntime runtime, ILogger<ReminderScheduler> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => ScheduleLoop(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    private async Task ScheduleLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var bus = new MessageBus(_runtime);
                var delay = (60 + Random.Shared.Next(0, 540)).Seconds();
                await bus.ScheduleAsync(new IncidentReminder(Guid.NewGuid(), "steady-state demo reminder"), delay);
                await Task.Delay(5.Seconds(), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to schedule an IncidentReminder");
                await Task.Delay(5.Seconds(), token);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }
}

/// <summary>No-op handler so scheduled <see cref="IncidentReminder"/> messages are consumed cleanly when due.</summary>
public static class IncidentReminderHandler
{
    public static void Handle(IncidentReminder reminder)
    {
        // no-op — these exist only to populate the Scheduled Messages view.
    }
}
