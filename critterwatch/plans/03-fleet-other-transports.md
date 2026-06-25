# 03 — `Fleet.{AmazonSqs, AzureServiceBus, Kafka, Redis, GooglePubSub}`

**Read `plans/README.md` + `01-fleet-rabbitmq-flagship.md` first. Each of these is the flagship with
the transport swapped.** Trip trio only (no Incidents — per the locked decision). Storage Marten/Postgres.

> **Initial scope = `AzureServiceBus` (✅ verified green), `Redis`, `Kafka`.** `AmazonSqs` was **pulled
> from this round** → [CritterStackSamples#5](https://github.com/JasperFx/CritterStackSamples/issues/5)
> (builds clean, Aspire+LocalStack runtime battery red — wiring preserved in the issue). `GooglePubSub` is
> **deferred** → [wolverine#3258](https://github.com/JasperFx/wolverine/issues/3258).

> **Spike resolved** — see `plans/09-FINDINGS.md` for the exact wiring + evidence. Verdicts below.
> The control channel rides two transport-agnostic levers on `ListenerConfiguration`: a per-endpoint
> `DefaultSerializer` (CritterWatch brotli-STJ serializer) + `.ListenOnlyAtLeader()` (a leader-pinned
> listener agent). **Cross-cutting requirement:** the DLQ + Scheduled panels read CritterWatch's durable
> `IMessageStore`, NOT a broker-native DLQ — so every fleet here must still pair its transport with a
> durable Marten/Postgres store for those panels to populate. The transport governs only control/telemetry.

| Solution | Transport pkg | Aspire container | Source to port | Status |
|----------|---------------|------------------|----------------|--------|
| `Fleet.AmazonSqs` | `WolverineFx.AmazonSqs` | LocalStack via `builder.AddContainer("localstack", "localstack/localstack")` (no first-class Aspire SQS resource) | `src/Samples/Trips3` | ✅ proven |
| `Fleet.AzureServiceBus` | `WolverineFx.AzureServiceBus` | `Aspire.Hosting.Azure.ServiceBus` + `.RunAsEmulator()` (needs a backing MSSQL container the emulator requires) | `src/Samples/Trips2` (+ `Trip2AsbConfig`) | ✅ proven |
| `Fleet.Redis` | `WolverineFx.Redis` | `Aspire.Hosting.Redis` → `builder.AddRedis("redis")` | flagship + Wolverine Redis docs | ✅ works as-is (stream consumer groups; AutoProvision idempotent) |
| `Fleet.Kafka` | `WolverineFx.Kafka` | `Aspire.Hosting.Kafka` → `builder.AddKafka("kafka")` | flagship + Wolverine Kafka docs | ⚠️ works — use a **single-partition control topic**; leader-only is enforced by the agent, not the broker; no native DLQ/scheduling |
| `Fleet.GooglePubSub` | `WolverineFx.Pubsub` | Pub/Sub emulator (later) | — | ⏭️ **DEFERRED — DO NOT BUILD NOW.** Blocked on upstream Wolverine **[#3258](https://github.com/JasperFx/wolverine/issues/3258)** (leader-pinned listeners get per-node subscriptions, breaking single-consumer). Build once that fix lands. See `09-FINDINGS.md` for detail. |

## Per-transport notes
- **SQS/GCP/Prometheus** have no first-class Aspire resource → use `AddContainer(...)` with explicit
  ports + env, `WithHttpEndpoint`, and a `WaitFor`. Annotate the container wiring (it's the interesting part).
- **ASB emulator** requires a SQL Edge/MSSQL sidecar and has a 50-entity cap — carry the `Trip2`
  constraints (`SystemQueuesEnabled=false`, explicit routing, management connection string).
- **Feature coverage varies by transport.** DLQ + scheduled-message panels are broker-native; on Kafka/Redis
  some CritterWatch panels will legitimately be empty. **Annotate this in the README** — don't hide it.
- Each service reads its broker connection from the Aspire-injected env with a localhost fallback.

## Tests / DoD
Per `plans/README.md`. For brokers without DLQ semantics, assert only the panels that apply.
