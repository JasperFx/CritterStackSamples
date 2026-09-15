using CritterWatch.Services;
using CritterWatch.Services.Hosting;
using Fisher;
using InventoryService;
using Wolverine;
using Wolverine.Fisher;
using Wolverine.Http;

// ============================================================================
// Embedded CritterWatch on SQLite — an ordinary ASP.NET Core app that mounts the
// monitoring console inside itself.
//
// No broker. No database server. No containers. Two SQLite files under the OS temp
// directory, created on first run. `dotnet run` and open the console.
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

var hostDatabase = Path.Combine(Path.GetTempPath(), "critterwatch-embedded-sample", "inventory.db");
var consoleDatabase = Path.Combine(Path.GetTempPath(), "critterwatch-embedded-sample", "critterwatch.db");
Directory.CreateDirectory(Path.GetDirectoryName(hostDatabase)!);

// The application's OWN store. Nothing here knows about CritterWatch.
//
// ⚠️ .IntegrateWithWolverine() on YOUR OWN store is a prerequisite of embedded mode, and not an
// obvious one: only the primary store's integration registers the machinery the console's ancillary
// store is routed through. Without it the host fails at startup with a message naming the CONSOLE's
// store, which points at the wrong half (CritterWatch #1209).
builder.Services
    .AddFisher(m => m.Connection($"Data Source={hostDatabase}"))
    .IntegrateWithWolverine();

builder.Host.UseWolverine(opts =>
{
    // One call. The console registers its own store, its own handlers, and monitoring of THIS host.
    opts.AddCritterWatchEmbedded($"Data Source={consoleDatabase}");
});

var app = builder.Build();

// The host's own endpoints, unaffected by the console.
app.MapPost("/inventory/receive", (ReceiveStock command, IMessageBus bus) => bus.InvokeAsync(command));

app.MapGet("/inventory/{id}", async (string id, IQuerySession session) =>
{
    var product = await session.LoadAsync<Product>(id);
    return product is null ? Results.NotFound() : Results.Ok(product);
});

// One call. Mounts the console's API and SPA under /critterwatch.
//
// ⚠️ It does NOT take over the host's route table: an unmatched host route still 404s, and the
// console's SPA fallback answers only under its own prefix. InventoryIsolationTests asserts that,
// because "the console swallowed my 404s" is the kind of regression a sample exists to catch.
app.UseCritterWatchEmbedded();

await app.RunAsync();

/// <summary>Exposed so the test project can boot this exact host with WebApplicationFactory.</summary>
public partial class Program;
