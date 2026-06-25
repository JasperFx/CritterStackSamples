# 05 — `WebService.Http`

**Read `plans/README.md` + `01-fleet-rabbitmq-flagship.md` first.** No transport-control-channel
upstream dependency — buildable now.

Demonstrates CritterWatch monitoring a **Wolverine HTTP web service** where the monitoring link rides
**Wolverine's HTTP transport** (not a broker). Storage Marten/Postgres.

## Prerequisite reading (`~/code/CritterWatch`)
- `Wolverine.CritterWatch.Http/**` — the HTTP-transport monitoring client package (`Wolverine.CritterWatch.Http`,
  packed at `1.0.0-alpha.4`). Read its public extension(s) — likely `AddCritterWatchHttp(...)` (#538).
- Any existing use of `AddCritterWatchHttp` in `src/Samples` (e.g. `ModularMonolith` references #538 for
  ASP.NET endpoint discovery) — grep `AddCritterWatchHttp`.
- `src/Smoke` for the console host shape.

## Build notes
- One ASP.NET + WolverineFx.Http web service (a small order/todo API) that registers HTTP-transport
  monitoring to the console. The console is the standalone `CritterWatch` console.
- AppHost provisions Postgres + launches the web service + the console; the service reaches the console
  over HTTP (`WithReference(console)` so service discovery injects the console URL).
- Confirm by reading the package whether the link is **push** (service → console HTTP endpoint) or **poll**
  (console scrapes the service); wire accordingly and annotate.

## Tests / DoD
Per `plans/README.md`, plus assert the HTTP web service's own endpoints respond and it appears in
`/api/critterwatch/services`.
