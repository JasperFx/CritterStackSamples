# 02 — `Embedded.Marten` + `Embedded.Polecat`

**Read `plans/README.md` + skim `01-fleet-rabbitmq-flagship.md` first.**

Two solutions showing CritterWatch **embedded** in a host app that monitors *itself* over `local://`
(no broker). Each host is a **modular monolith**: TripService + TripPublisher + RepairShop collapsed
into one app, where **RepairShop's storage is an ancillary store** (showing modular-monolith store
isolation). CritterWatch attaches its own ancillary store via `hostOwnsPrimaryStore: true`.

| Solution | Host primary store | RepairShop ancillary | CritterWatch |
|----------|--------------------|----------------------|--------------|
| `Embedded.Marten` | Marten/Postgres | Marten ancillary (`AddMartenStore<IRepairShopStore>`) | `AddCritterWatchEmbedded(hostOwnsPrimaryStore: true)` |
| `Embedded.Polecat` | Polecat/SQL Server | Polecat ancillary (`AddPolecatStore<IRepairShopStore>`) | embedded, Polecat flavor |

## Prerequisite reading (`~/code/CritterWatch`)
- `src/Samples/EmbeddedDemo/EmbeddedDemo.Marten/Program.cs` + `EmbeddedDemo.Polecat/Program.cs` —
  the embedded self-monitoring pattern (`AddCritterWatchEmbedded` + `MapCritterWatchEmbedded`).
- `src/Samples/ModularMonolith/Program.cs` — modules in one host, local-queue inter-module messaging.
- `src/Samples/MultiStoreHost/**` — the ancillary typed-store registration pattern.
- `src/CritterWatch.Services/Hosting/CritterWatchEmbeddedExtensions.cs` — the exact embedded API.

## Build notes
- One web app per solution: three "modules" (TripBuilding, Publishing, RepairShop). Inter-module
  messages travel **local queues**; RepairShop persists to its **own ancillary store/schema**.
- Embed CritterWatch: `opts.AddCritterWatchEmbedded(conn, hostOwnsPrimaryStore: true, schemaName: "critterwatch")`
  then `app.MapCritterWatchEmbedded()`. The host owns the primary store; CritterWatch only adds its ancillary.
- **AppHost** provisions one Postgres (Marten) or one SQL Server 2025 (Polecat) container + the single app.
  No broker needed (self-monitoring is `local://`).
- ⚠️ **`CritterWatch.Embedded` package fails to pack (NU5026).** Two options, prefer (a):
  (a) the embedded extensions live in the **`CritterWatch` / `CritterWatch.SqlServer`** packages already —
  reference those, no `CritterWatch.Embedded` needed; OR
  (b) **in the dedicated worktree `~/code/_cw_worktrees/samples-support` (NOT the main CritterWatch copy —
  see README "CritterWatch-side changes")**, fix `src/CritterWatch.Embedded/CritterWatch.Embedded.csproj` to add the empty
  `<TargetFramework></TargetFramework>` + `<TargetFrameworks>net9.0;net10.0</TargetFrameworks>` trick
  (copy from `CritterWatch.Services.SqlServer.csproj`), re-pack, then reference it.
  Determine which package actually exposes `AddCritterWatchEmbedded` by reading the source, and reference that.

## Tests / DoD
Per `plans/README.md`. The fleet here is the single embedded host monitoring itself — assert `/services`
shows the host as a registered (self-)service and the dashboard is served from the embedded SPA.
