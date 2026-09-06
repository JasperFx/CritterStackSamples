using CritterWatch.Services;
using CritterWatch.Services.Hosting;
using Fisher;
using InventoryService;
using Wolverine;
using Wolverine.Fisher;
using Wolverine.Http;

// =============================================================================================
// InventoryService — an ordinary application that EMBEDS the CritterWatch console.
//
// There is no second process, no broker, and no database server: Fisher writes SQLite files, so this
// whole sample is `git clone` then F5. That is the point of embedded mode — the console is a
// development-time window into the app you are already running.
// =============================================================================================

var builder = WebApplication.CreateBuilder(args);

// Two SQLite files, and the separation is the whole demonstration:
//   inventory.db   — the HOST's documents. CritterWatch never writes here.
//   critterwatch.db — the console's own store.
var hostDb = Path.Combine(AppContext.BaseDirectory, "inventory.db");
var consoleDb = Path.Combine(AppContext.BaseDirectory, "critterwatch.db");

// The host's OWN store. Nothing here mentions CritterWatch.
builder.Services.AddFisher(opts =>
{
    opts.ConnectionString = $"Data Source={hostDb}";
}).IntegrateWithWolverine();

builder.Host.UseWolverine(opts =>
{
    opts.ServiceName = "InventoryService";

    // begin-snippet: embedded-registration
    // ONE call. The console registers its own ancillary store, routes its own handlers and HTTP
    // endpoints to it, and leaves this application's runtime alone — same service name, same handler
    // discovery, same listeners.
    opts.AddCritterWatchEmbedded($"Data Source={consoleDb}");
    // end-snippet
});

var app = builder.Build();

// The host's own routes. These must keep working, and keep 404ing where they always did.
app.MapGet("/inventory/{id:guid}", async (Guid id, IQuerySession session) =>
    await session.LoadAsync<Product>(id) is { } p ? Results.Ok(p) : Results.NotFound());

app.MapPost("/inventory/receive", async (ReceiveStock command, IMessageBus bus) =>
{
    await bus.InvokeAsync(command);
    return Results.Accepted();
});

// begin-snippet: embedded-mounting
// Mounts the console's UI under /critterwatch. It does NOT install a root SPA fallback and does NOT
// map health endpoints — this application's route table stays its own.
app.UseCritterWatchEmbedded();
// end-snippet

app.Run();

// Exposed so the test project can boot this exact application rather than a copy of it.
public partial class Program;
