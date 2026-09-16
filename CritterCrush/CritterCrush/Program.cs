using Bobcat.EventModel;
using CritterCrush.Scheduling;
using JasperFx;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWolverineHttp();

// The declared model, alongside the one Wolverine and Marten derive from the code. Both feed the
// same merged EventModelDescriptor, and where they disagree the merge says so — which is how this
// application answers "does the code still do what the board said" without anybody reading both.
builder.Services.AddEventModelFile(
    Path.Combine(builder.Environment.ContentRootPath, "..", "models", "CritterCrush.emodel.yaml"));

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
        opts.Projections.Snapshot<VolunteerApplication>(SnapshotLifecycle.Inline);
        opts.Projections.Snapshot<HomeCheck>(SnapshotLifecycle.Inline);

        // Both are ASYNC. Both are also multi-stream — the queue folds every appointment stream in
        // a shelter into one document, the page every stream an owner has — so neither could be an
        // Inline snapshot anyway. Inline would run inside every slice's write transaction, so one
        // unfilled Apply would fail every OTHER slice's command — coupling nine slices to the
        // progress of one. Async keeps the blast radius to the projection: the daemon stops on the
        // unfilled event, and only the scenarios asserting that read model fail, on their
        // projection wait. Scaffolded projections register cleanly as of Bobcat 0.13.0 (#232).
        opts.Projections.Add<AppointmentsQueueProjection>(ProjectionLifecycle.Async);
        opts.Projections.Add<MyAppointmentsProjection>(ProjectionLifecycle.Async);

        // Volunteering's one view is SINGLE-stream — one document per application, folded from that
        // application's own stream — and still async, for the same blast-radius reason.
        opts.Projections.Add<VolunteerApplicationsQueueProjection>(ProjectionLifecycle.Async);
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
