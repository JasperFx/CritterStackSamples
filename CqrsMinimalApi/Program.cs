using JasperFx;
using JasperFx.CodeGeneration;
using Marten;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Production code-generation defaults (the point of this sample) ──────────────────────────
// In the Production environment, run the *pre-generated* code via TypeLoadMode.Static and assert
// that every expected generated type is present — so the app boots with NO runtime Roslyn
// compilation. In Development, the default (Dynamic) mode compiles handlers on the fly using the
// WolverineFx.RuntimeCompilation package, which is referenced ONLY in Debug builds (see the
// .csproj). Production deployments publish in Release, which drops that package + Roslyn entirely.
builder.Services.CritterStackDefaults(x =>
{
    x.Production.GeneratedCodeMode = TypeLoadMode.Static;
    x.Production.AssertAllPreGeneratedTypesExist = true;
});

builder.Services.AddWolverineHttp();

builder.Services.AddMarten(opts =>
{
    var connectionString = builder.Configuration.GetConnectionString("Marten")
        ?? "Host=localhost;Port=5433;Database=cqrs_minimal_api;Username=postgres;Password=postgres";

    opts.Connection(connectionString);
    opts.DatabaseSchemaName = "cqrs_demo";

    // Use HiLo sequence for int-based identity (matching the original project's int IDs)
    opts.Schema.For<CqrsMinimalApi.Student>().UseNumericRevisions(true);
    opts.Schema.For<CqrsMinimalApi.Student>().Index(x => x.Name);
})
.IntegrateWithWolverine()
.UseLightweightSessions();

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    opts.Policies.AutoApplyTransactions();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapWolverineEndpoints();

// Seed only for an actual server run. CLI commands (e.g. `codegen write`) boot the host in
// metadata-only mode without a database, so skip seeding when arguments are present.
if (args.Length == 0)
{
    await SeedData(app);
}

// Route command-line args so `dotnet run -- codegen write` (and `codegen delete`, `check-env`,
// `describe`, …) work; with no args this just runs the web host.
return await app.RunJasperFxCommands(args);

static async Task SeedData(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

    var existing = await session.Query<CqrsMinimalApi.Student>().AnyAsync();
    if (!existing)
    {
        session.Store(
            new CqrsMinimalApi.Student { Name = "Tonny Blatt", Address = "123 Main St", Email = "tonny@example.com", DateOfBirth = new DateTime(1991, 10, 7) },
            new CqrsMinimalApi.Student { Name = "Anitta Goldman", Address = "456 Oak Ave", Email = "anitta@example.com", DateOfBirth = new DateTime(1975, 5, 31) },
            new CqrsMinimalApi.Student { Name = "Alan Ford", Address = "789 Pine Rd", Email = "alan@example.com", DateOfBirth = new DateTime(2000, 8, 26) },
            new CqrsMinimalApi.Student { Name = "Jim Beam", Address = "321 Elm St", Email = "jim@example.com", DateOfBirth = new DateTime(1984, 1, 12) },
            new CqrsMinimalApi.Student { Name = "Suzanne White", Address = "654 Birch Ln", Email = "suzanne@example.com", DateOfBirth = new DateTime(1992, 3, 10) }
        );
        await session.SaveChangesAsync();
    }
}
