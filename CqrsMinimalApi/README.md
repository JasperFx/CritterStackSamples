# CQRS Minimal API — Critter Stack Conversion

## Original Project

**Repository:** [matjazbravc/CQRS.MinimalAPI.Demo](https://github.com/matjazbravc/CQRS.MinimalAPI.Demo)
**License:** MIT
**Stack:** ASP.NET Core 8 Minimal API, MediatR 12, EF Core 8 (SQLite), MiniValidation

A CQRS demonstration using MediatR for command/query dispatch with a Student management domain. Shows the typical MediatR + Minimal API wiring pattern: endpoints call a service layer, which creates MediatR commands/queries and sends them through the mediator.

## What Changed

### Removed (6 layers of indirection)
- **MediatR** — 3 command classes, 3 query classes, 6 handler classes
- **Service layer** — `IStudentsService` / `StudentsService` mediator bridge
- **Repository layer** — `IBaseRepository<T>`, `BaseRepository<T>`, `IStudentsRepository`, `StudentsRepository`
- **EF Core** — `DataContext`, SQLite database, migrations
- **MiniValidation** — replaced by standard ASP.NET model binding

### Added
- **Wolverine.Http** — `[WolverineGet]`, `[WolverinePost]`, etc. endpoint attributes
- **Marten** — PostgreSQL document store, `IDocumentSession` / `IQuerySession`

### Before vs After

| Aspect | Original (MediatR) | Converted (Wolverine + Marten) |
|--------|-------------------|-------------------------------|
| Files | 22 C# files across 8 directories | 3 C# files in 1 directory |
| Request flow | Endpoint → Service → MediatR → Handler → Repository → EF Core | Endpoint → Marten session |
| Database | SQLite via EF Core + migrations | PostgreSQL via Marten (auto-schema) |
| Packages | MediatR, EF Core, MiniValidation | WolverineFx.Http, Marten |
| Identity type | `int` (auto-increment) | `int` (Marten HiLo) |
| Tests | None | Alba integration tests |

### Architecture

The original project demonstrated a 6-layer call chain for every operation:

```
Endpoint → IStudentsService → IMediator.Send() → IRequestHandler → IStudentsRepository → DbContext
```

The converted project collapses this to a single Wolverine HTTP endpoint method that directly uses Marten's document session:

```
[WolverinePost] endpoint method → IDocumentSession
```

Each endpoint is a static method in `StudentEndpoints.cs` with Wolverine HTTP attributes. Marten's `IDocumentSession` and `IQuerySession` are injected directly — no repository abstraction needed since Marten already provides a clean document store API.

### Bug Fix

The original `StudentsService.Create()` had Address and Email swapped in the command mapping. This was corrected in the conversion.

## Running

Requires PostgreSQL. Update the connection string in `appsettings.json`, then:

```bash
dotnet run
```

Swagger UI available at `/swagger`.

## Production code generation without Roslyn

This sample also shows, end to end, how to run in production with **`TypeLoadMode.Static`** and
**without** the `WolverineFx.RuntimeCompilation` package (and its Roslyn dependencies), while still
using runtime compilation during local development — the workflow requested in
[wolverine#2900](https://github.com/JasperFx/wolverine/issues/2900).

| | Development | Production |
|---|---|---|
| Build configuration | `Debug` | `Release` |
| `WolverineFx.RuntimeCompilation` (Roslyn) | **referenced** (Debug-only) | **excluded** |
| Code generation | `Dynamic` — compiled at startup | `Static` — runs pre-generated code |

The package reference is a **build-time** decision (keyed off `$(Configuration)`); the code-gen mode
is a **runtime** decision (the `Production` environment profile). They line up: publish `Release`, run
as `Production`.

### Moving parts
1. **`Program.cs`** — `CritterStackDefaults(x => { x.Production.GeneratedCodeMode = TypeLoadMode.Static; x.Production.AssertAllPreGeneratedTypesExist = true; })`, and `return await app.RunJasperFxCommands(args)` so `dotnet run -- codegen write` works.
2. **`CqrsMinimalApi.csproj`** — `WolverineFx.RuntimeCompilation` is referenced inside `<ItemGroup Condition="'$(Configuration)' == 'Debug'">`. ⚠️ The parentheses in `$(Configuration)` matter — a malformed condition like `'$Configuration)'` silently never matches, so the assembly keeps shipping (the symptom in #2900).
3. **`Internal/Generated/`** — pre-generated code from `codegen write` (committed so `Release` builds/tests without Roslyn). `codegen write` boots in metadata-only mode, so **no database** is needed.

### Prove it
```bash
dotnet run -- codegen write          # (re)generate code — no DB required
./verify-production-build.sh         # publish Release; assert NO Wolverine.RuntimeCompilation / Microsoft.CodeAnalysis* / JasperFx.RuntimeCompiler
dotnet test Tests                    # ProductionStaticCodegenTests boots with ASPNETCORE_ENVIRONMENT=Production (Static)
docker build -t cqrs-minimal-api -f Dockerfile .   # codegen write -> publish Release -> Roslyn-free image
```

### FAQ (from #2900)
- **Is `AssertAllPreGeneratedTypesExist` the default?** No — it defaults to `false`; set it `true` so a missing/stale pre-generated type fails fast at startup.
- **Why did my conditional `PackageReference` still ship the assembly?** Almost certainly a malformed condition (`$Configuration)` missing the leading `(`). Use the form above and confirm with `verify-production-build.sh`.
- **Must I commit `Internal/Generated/`?** No — the `Dockerfile` regenerates it during the build; committing is just for convenience.
