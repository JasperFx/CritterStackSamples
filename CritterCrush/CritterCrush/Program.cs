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
        opts.DatabaseSchemaName = "crittercrush";

        // The write model read back by id gets an Inline snapshot: a caller's next GET sees
        // their own write, and the automations aggregate against committed state.
        opts.Projections.Snapshot<Appointment>(SnapshotLifecycle.Inline);

        // Both read models group by an identity that is not the stream id, so both are
        // multi-stream — and Async means the daemon below is load-bearing.
        //
        // NOT REGISTERED YET, deliberately: the model marks both View slices unrealized (no bound
        // specifications), and Marten validates a projection at startup — an unfilled one has no
        // slicing rules and no Apply methods, so registering it stops the host booting and takes
        // every other slice's specs down with it (bobcat#232). Register each one as its slice is
        // built and its specs bound.
        // opts.Projections.Add<AppointmentsQueueProjection>(ProjectionLifecycle.Async);
        // opts.Projections.Add<MyAppointmentsProjection>(ProjectionLifecycle.Async);
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
