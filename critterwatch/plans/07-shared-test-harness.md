# 07 — Shared Aspire test harness

**Read `plans/README.md` first. Build this before/alongside the flagship — every solution's `Tests/`
depends on it.**

A small shared library + xUnit helpers that each solution's `Tests/` project references, so the
"launch the AppHost and assert CritterWatch routes respond" battery isn't re-implemented per solution.

## Deliverable
`critterwatch/Shared/CritterWatch.Samples.Testing/` — a class library (net10.0, CPM) referencing
`Aspire.Hosting.Testing`, `xunit`, `Shouldly`.

Provide:
- A fixture that boots an AppHost via `DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>()`
  → `builder.BuildAsync()` → `app.StartAsync()`, and exposes an `HttpClient` for the `critterwatch`
  resource (`app.CreateHttpClient("critterwatch")`). Generic over the AppHost type so each solution
  passes its own `Projects.*AppHost`.
- A `ResourceNotificationService.WaitForResourceAsync("critterwatch", KnownResourceStates.Running)` wait
  with a generous timeout.
- Assertion helpers:
  - `await client.CritterWatchAboutOk()` → `GET /api/critterwatch/about` is 200.
  - `await client.WaitForServicesAsync(expectedNames, timeout)` → polls `GET /api/critterwatch/services`
    until all expected service names appear (services self-register asynchronously — **poll, don't
    assert once**), then returns the parsed list.

## Notes
- These tests **start real containers** → they need Docker available. Mark them with a trait/collection so
  they can be filtered in environments without Docker. Note in the README that CI needs Docker-in-CI.
- Keep solution `Tests/` projects thin: one fixture instantiation + a handful of asserts using the helpers.
- Confirm the exact JSON shape of `/api/critterwatch/services` by reading
  `~/code/CritterWatch/src/CritterWatch.Services/Endpoints/CritterWatchEndpoint.cs` (or the generated
  `GET_api_critterwatch_services` handler) so the parse matches.
