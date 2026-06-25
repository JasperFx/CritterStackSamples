# 06 — `Metrics.Prometheus`

**Read `plans/README.md` + `01-fleet-rabbitmq-flagship.md` first.** No upstream dependency —
the Prometheus-poll path is **already implemented server-side**. Buildable now.

Demonstrates the **external-metrics** posture: the fleet services run in
`WolverineMetricsMode.SystemDiagnosticsMeter` (they push **no** metrics to CritterWatch and instead
export Prometheus metrics), a Prometheus container scrapes them, and **CritterWatch polls Prometheus**
for the dashboards/alerts. Contrast: the other fleets use native Wolverine metric push.

## Prerequisite reading (`~/code/CritterWatch`)
- `src/CritterWatch.Services/Metrics/PrometheusMetricsDataSource.cs`,
  `PrometheusScrapingService.cs`, `ServiceCollectionMetricsDataSourceExtensions.cs`
  (`AddCritterWatchMetricsDataSource<PrometheusMetricsDataSource, PrometheusMetricsDataSourceOptions>(...)`
  + `SetDefaultCritterWatchMetricsDataSource(...)`).
- `Wolverine.CritterWatch/CritterWatchOptions.cs` → `.MetricsDataSource("prometheus")` (monitored-side
  declared preference; materializes into a `ServiceMetricsDataSourceBinding`).
- The `WolverineMetricsMode.SystemDiagnosticsMeter` switch on the monitored side, and
  `ServiceSummary.ExternalMetricsOnly` (#469 Track B) for the "external metrics" badge.

## Build notes
- Base on `Fleet.RabbitMq` (Trip trio over Rabbit) **but**: services set the meter-only metrics mode +
  add the OpenTelemetry Prometheus exporter (`OpenTelemetry.Exporter.Prometheus.AspNetCore`); the console
  registers the Prometheus data source and sets it default, then binds it for the services
  (or services declare `.MetricsDataSource("prometheus")`).
- **AppHost** adds a Prometheus container via `AddContainer("prometheus", "prom/prometheus")` with a
  mounted `prometheus.yml` that scrapes each service's `/metrics`. Wire `WaitFor` + the console's
  `PrometheusMetricsDataSourceOptions.BaseUrl` to the Prometheus container endpoint.
- Annotate the contrast with native push clearly — this sample's whole point is the external-metrics path.

## Tests / DoD
Per `plans/README.md`, plus assert a service shows `ExternalMetricsOnly` posture and that metrics
surface for it (poll `/api/critterwatch/...` metrics route after Prometheus has a scrape interval or two).
