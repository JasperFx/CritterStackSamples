# Embedded.Sqlite

CritterWatch running **inside your own application**, on **SQLite**. No broker, no database server,
no containers — `dotnet run` and open the console.

This is the sample to read when you want monitoring for one service without deploying a second
process to host it.

## What it shows

- A small **inventory API** (`InventoryService`) on **Wolverine + Fisher/SQLite**, with its own
  `Product` document and handler.
- The **CritterWatch console embedded in that same application**, monitoring it, mounted at
  `/critterwatch`.
- The isolation that makes this safe: the console's data in **its own** SQLite file, the host's
  routes untouched, the host's own store untouched.

Two calls:

```csharp
opts.AddCritterWatchEmbedded($"Data Source={consoleDatabase}");   // inside UseWolverine
app.UseCritterWatchEmbedded();                                    // mounts API + SPA
```

## Run it

```bash
dotnet run --project InventoryService
```

Then:

- <http://localhost:5000/critterwatch> — the console
- `POST /inventory/receive` with `{"productId":"widget","name":"Widget","quantity":5}`
- `GET /inventory/widget`

Two SQLite files appear under your temp directory: `inventory.db` (yours) and `critterwatch.db`
(the console's). Delete them to start over.

## What the tests assert, and why each one is here

`dotnet test` — six assertions, each mapping to a claim on the
[embedded deployment page](https://critterwatch.jasperfx.net/deployment/embedded).

| assertion | why it exists |
|---|---|
| the host's own endpoints still work | embedding a console must not change your app |
| the console's API answers under `/api/critterwatch` | it is actually running |
| **the console's SPA is mounted at `/critterwatch`** | pairs with the next one |
| **an unmatched host route still 404s** | the console must not swallow your 404s |
| the console's documents are in **its own** database, and none are in the host's | the isolation claim |
| the console has **its own** `wolverine_*` envelope tables | documents reality — see below |

⚠️ **The two SPA assertions only mean something together.** Alone, "an unmatched route 404s" passes
just as happily against a build with no embedded SPA at all — the fallback middleware would no-op and
every route would 404 for the wrong reason. Together they say *the SPA is served here and nowhere
else*. This pair is the reason the assertion lives in this sample rather than in CritterWatch's own
test suite, where the SPA is not embedded by default and the test would be permanently vacuous.

## ⚠️ The console gets its own `wolverine_*` tables

Its documents **and** its Wolverine envelope tables are in `critterwatch.db`. Envelope sharing with
your application happens only when the host sets `opts.Durability.MessageStorageSchemaName` — which
is **not** the same property as the `MessageStorageSchemaName` you set inside
`IntegrateWithWolverine(o => ...)`. This sample deliberately does not set it, because not setting it
is the ordinary case.

**What that means for you:** if you run a maintenance routine that resets or rebuilds the message
store, iterate every store rather than just the main one:

```csharp
foreach (var store in await host.GetRuntime().Stores.FindAllAsync())
{
    await store.Admin.RebuildAsync();
}
```

## ⚠️ Prerequisites that are not obvious

- **`.IntegrateWithWolverine()` on your own store.** Only the *primary* store's integration registers
  the machinery the console's ancillary store is routed through. Without it the host fails at startup
  with a message naming the *console's* store, which points at the wrong half.
- **`WolverineFx.RuntimeCompilation`.** The host owns the runtime, so the console generates its
  chains at runtime rather than using its own precompiled ones.
- **Single instance.** Embedded self-monitoring rides in-process `local://` messaging, which cannot
  cross nodes. On SQLite that is permanent — there is no queue transport for a second node's
  telemetry to travel over. Scale out and the console will tell you so.

## 🔧 Until CritterWatch 1.1 publishes

Embedded mode ships in 1.1 and is not on nuget.org yet, so this sample pins the CritterWatch packages
to a **local folder feed** via `Directory.Packages.props` + `nuget.config` beside this README. Every
other sample in this island is clone-and-F5 against nuget.org alone.

**When 1.1 publishes, delete those two files.** The sample then inherits the island's parent
`Directory.Packages.props` and needs nothing else. That is the whole migration.

To build the feed, from a CritterWatch checkout:

```bash
V=1.1.0-embedded.5
for p in src/Wolverine.CritterWatch/Wolverine.CritterWatch.csproj \
         src/CritterWatch.Services/CritterWatch.Services.csproj \
         src/CritterWatch.Services.Fisher/CritterWatch.Services.Fisher.csproj; do
  dotnet pack $p -c Release -o ~/code/_cw_localfeed \
    -p:EmbedFrontend=true -p:SkipFrontendBuild=true -p:Version=$V --nologo
done
```

`EmbedFrontend=true` is what puts the console's SPA in the package; without it the UI mounts but
serves nothing — and the two SPA assertions above are what catch that.

Bump the version each time you re-pack: a stale pack in the NuGet global cache silently shadows a
newer one of the same version.
