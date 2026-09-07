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

        // NEITHER IS REGISTERED YET, deliberately. Marten validates a projection at startup, and
        // a scaffolded one has no Apply methods — registering it stops the host booting and takes
        // every other slice's specs down with it (bobcat#232). Each registration below belongs to
        // the slice that fills its projection in, added in the same change.
        //
        // AppointmentsQueue is one row per appointment, keyed by the appointment's own stream, so
        // it is single-stream and Inline — no daemon latency between the write and the assertion:
        // opts.Projections.Add<AppointmentsQueueProjection>(ProjectionLifecycle.Inline);
        //
        // MyAppointments folds one document per OWNER across every appointment stream — a genuine
        // fan-out, so Async, and the daemon above is load-bearing for it. It has no specs yet
        // because the shipped grammar cannot address a document keyed by anything but the
        // scenario's stream id (bobcat#236):
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
