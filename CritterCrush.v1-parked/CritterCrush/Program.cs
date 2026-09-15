using CritterCrush.Discovery;
using CritterCrush.Profiles;
using JasperFx;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Projections;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWolverineHttp();

builder.Services.AddMarten(opts =>
    {
        var connectionString = builder.Configuration.GetConnectionString("Marten")
                               ?? "Host=localhost;Port=5433;Database=crittercrush;Username=postgres;Password=postgres";

        opts.Connection(connectionString);
        opts.DatabaseSchemaName = "crittercrush";

        // The write models read back by id get Inline snapshots: a caller's next GET sees their
        // own write, and the DetectMutualMatch automation aggregates against committed state.
        opts.Projections.Snapshot<DogProfile>(SnapshotLifecycle.Inline);
        opts.Projections.Snapshot<SwipePair>(SnapshotLifecycle.Inline);

        // The fan-out read model is Async on purpose — and Async means the daemon below is
        // load-bearing: without it this projection would silently never advance.
        opts.Projections.Add<MatchListProjection>(ProjectionLifecycle.Async);
    })
    // The same-module automation trigger: events appended through the outboxed session are
    // published to the Wolverine bus on commit, where DetectMutualMatchHandler receives the
    // unwrapped DogLiked. (Fast forwarding trades strict ordering for latency — right for this
    // decision, which aggregates its own stream anyway; use a subscription when order matters.)
    .IntegrateWithWolverine(m => m.UseFastEventForwarding = true)
    .AddAsyncDaemon(DaemonMode.Solo)
    .UseLightweightSessions();

builder.Host.UseWolverine(opts =>
{
    opts.ServiceName = "CritterCrush";
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    // Handlers never call SaveChangesAsync — the transactional middleware owns the commit, and
    // cascaded messages ride the same transaction through the outbox.
    opts.Policies.AutoApplyTransactions();
    opts.Policies.UseDurableLocalQueues();

    // Modular-monolith safety rails, on from day one rather than after the first doubled or
    // dropped message: two modules handling one event type stay independent chains, and a
    // message fanned out to several durable inboxes is deduplicated per destination.
    opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
    opts.Durability.MessageIdentity = MessageIdentity.IdAndDestination;
});

var app = builder.Build();

app.MapWolverineEndpoints();

return await app.RunJasperFxCommands(args);

// Alba (and the Bobcat spec host) binds to the app through this.
public partial class Program;
