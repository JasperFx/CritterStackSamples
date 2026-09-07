using CritterCrush.Appointments;
using JasperFx;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWolverineHttp();

builder.Services.AddMarten(opts =>
    {
        opts.Connection(builder.Configuration.GetConnectionString("Marten")
                        ?? "Host=localhost;Port=5433;Database=crittercrush;Username=postgres;Password=postgres");
        // Configurable so a parallel build can isolate itself: every spec run resets the event
        // store, so two agents sharing one schema wipe each other's data mid-run. See
        // CritterCrush.Specs/SuiteConfiguration.cs.
        opts.DatabaseSchemaName = builder.Configuration["Marten:SchemaName"] ?? "crittercrush";

        // The write model read back by id gets an Inline snapshot: a caller's next GET sees
        // their own write, and the automations aggregate against committed state.
        opts.Projections.Snapshot<Appointment>(SnapshotLifecycle.Inline);

        // Both are ASYNC, including AppointmentsQueue, which is single-stream and would otherwise
        // be a natural Inline. Inline would run inside every slice's write transaction, so one
        // unfilled Apply would fail every OTHER slice's command — coupling nine slices to the
        // progress of one. Async keeps the blast radius to the projection: the daemon stops on the
        // unfilled event, and only the scenarios asserting that read model fail, on their
        // projection wait. Scaffolded projections register cleanly as of Bobcat 0.13.0 (#232).
        opts.Projections.Add<AppointmentsQueueProjection>(ProjectionLifecycle.Async);
        opts.Projections.Add<MyAppointmentsProjection>(ProjectionLifecycle.Async);
    })
    .IntegrateWithWolverine(m => m.UseFastEventForwarding = true)
    .AddAsyncDaemon(DaemonMode.Solo)
    .UseLightweightSessions();

builder.Host.UseWolverine(opts =>
{
    opts.ServiceName = "CritterCrush";
    opts.Discovery.IncludeAssembly(typeof(Appointment).Assembly);

    opts.Policies.AutoApplyTransactions();
    opts.Policies.UseDurableLocalQueues();

    opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
    opts.Durability.MessageIdentity = MessageIdentity.IdAndDestination;
});

var app = builder.Build();
app.MapWolverineEndpoints();
return await app.RunJasperFxCommands(args);

public partial class Program;
