using JasperFx;
using JasperFx.Events.Projections;
using Library;
using Marten;
using Marten.Events.Projections;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMarten(opts =>
    {
        opts.Connection(builder.Configuration.GetConnectionString("Marten")!);
        opts.DatabaseSchemaName = "library";

        // Same two read models as the console quickstart, same Evolve() methods
        opts.Projections.Snapshot<Book>(SnapshotLifecycle.Inline);
        opts.Projections.Snapshot<BorrowedBook>(SnapshotLifecycle.Inline);
    })
    // Wolverine's transactional middleware + outbox around Marten sessions. Fast event
    // forwarding publishes each appended event to Wolverine handlers as the session commits,
    // which is how the "reactor" below gets its BookReturned without an async daemon.
    .IntegrateWithWolverine(x => x.UseFastEventForwarding = true)
    .UseLightweightSessions();

builder.Services.AddWolverineHttp();

builder.Host.UseWolverine(opts =>
{
    opts.Policies.AutoApplyTransactions();
    opts.ServiceName = "LibraryQuickstart";
});

var app = builder.Build();

app.MapWolverineEndpoints();

return await app.RunJasperFxCommands(args);
