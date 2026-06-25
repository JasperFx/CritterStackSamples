# 09 — Findings: control-channel parity (Kafka / Redis / GCP / DB queues)

**Read-only spike output.** Gates `03-fleet-other-transports.md` (Kafka/Redis/GCP) and
`04-fleet-db-queues.md`. Investigation spanned `~/code/CritterWatch` and `~/code/wolverine`.

---

## How the control channel works today (the pattern we're porting)

CritterWatch's monitoring rides **two endpoints** wired by
`AddCritterWatchMonitoring(critterWatchUri, systemControlUri)` in
`Wolverine.CritterWatch/WolverineOptionsExtensions.cs`:

1. **Telemetry route (`critterWatchUri`)** — outbound. Every `ICritterWatchMessage`, the Wolverine
   `MessageHandlingMetrics`, and `WolverineHeartbeat` are `options.Publish(...)`-routed *to* the
   shared `critterwatch` queue with the CritterWatch serializer pinned
   (`WolverineOptionsExtensions.cs:182-198, 254-271`). The BFF/console listens on the same queue
   name.
2. **Control listener (`systemControlUri`)** — inbound. The monitored service listens for commands
   FROM CritterWatch:
   ```csharp
   // WolverineOptionsExtensions.cs:141-152
   var controlListener = options.ListenForMessagesFrom(systemControlUri)
       .DefaultSerializer(critterWatchSerializer);
   if (IsDatabaseQueueScheme(systemControlUri.Scheme))   // #531
       controlListener.BufferedInMemory();
   if (!string.Equals(systemControlUri.Scheme, "local", ...))
       controlListener.ListenOnlyAtLeader();              // leader-only gate
   ```

**Three load-bearing facts that determine portability:**

- **The CritterWatch serializer is a per-endpoint `DefaultSerializer`** — a brotli-framed STJ
  serializer (`BuildCritterWatchSerializer()`, `WolverineOptionsExtensions.cs:434-438`). On the
  console side it's auto-applied by `CritterWatchSerializerPolicy`
  (`src/CritterWatch.Services/CritterWatchSerializerPolicy.cs`) to any endpoint whose name matches
  `critterwatch` or that carries an `ICritterWatchMessage`/`IServiceMessage` subscription, or
  explicitly via `.UseCritterWatchSerializer()` (`src/CritterWatchBff/Program.cs:53,69,104`).
  `DefaultSerializer` is defined on the generic
  `Wolverine/Configuration/ListenerConfiguration.cs:410-419`, so **any transport whose listener
  config derives from the generic `ListenerConfiguration<TSelf,TEndpoint>` supports it for free.**

- **`ListenOnlyAtLeader()` is generic and transport-agnostic.** It sets
  `ListenerScope.PinnedToLeader` (`ListenerConfiguration.cs:202-211`). At runtime,
  `LeaderPinnedListenerFamily` (`Wolverine/Runtime/Agents/LeaderPinnedAgentFamily.cs`) turns each
  pinned listener into an agent and `assignments.RunOnLeader(agentUri)` (line 114). Crucially
  `LeaderPinnedListenerAgent.StartAsync` only calls
  `_runtime.Endpoints.StartListenerAsync(_endpoint, …)` on the leader node (line 25-28) — **so the
  consumer/subscriber for the control endpoint is created on exactly one node.** The agent Uri
  already namespaces by transport scheme (`GH-3027`, line 19-22), so `critterwatch` on two
  transports won't collide. Single-node / `Solo` hosts skip the gate entirely (the `!local` guard +
  `LeaderExecution.ShouldExecuteHereAsync` non-Balanced shortcut,
  `Handlers/LeaderExecution.cs:40-43`).

- **Leader-follower forwarding** (`Handlers/LeaderExecution.cs:60-62`) forwards an agent-lifecycle
  command to `leader.ControlUri` — that's the **Wolverine node-record control endpoint**, not our
  `systemControlUri`. Independent of the monitoring transport; no per-transport work.

**Wiring proven today** (samples consume these as-is):
| Transport | `critterWatchUri` | `systemControlUri` | Source |
|-----------|-------------------|--------------------|--------|
| RabbitMQ | `rabbitmq://queue/critterwatch` | `rabbitmq://queue/trip_service` | `src/Samples/Trips/TripService/Program.cs:152-154` |
| Azure Service Bus | `asb://queue/critterwatch` | `asb://queue/trip2_service` | `src/Samples/Trips2/Trip2Service/Program.cs:129-131` |
| Amazon SQS | `sqs://critterwatch` | `sqs://trip3_service` | `src/Samples/Trips3/Trip3Service/Program.cs:104-109` |
| Postgres/SQL queue | `postgresql://critterwatch` | `postgresql://my-service-control` | `src/DocSamples/TransportChannelsSamples.cs:33-43` |

These three brokers share the property the pattern depends on: a **queue** (competing-consumer) the
producer addresses by name, where a single leader-pinned listener drains it. Kafka/Redis/Pub/Sub are
topic/stream-shaped, so the question per transport is whether a single-consumer, leader-owned,
auto-provisioned, custom-serializer endpoint is expressible.

---

## Already-solved upstream items (do NOT re-investigate)

Cross-referenced from CritterWatch memory + repo evidence:

- **#531 DB-queue control channel — RESOLVED.** `wolverine#3248` (PRs #3249/#3250/#3251) +
  `#3226` reconciliation seam ship in the `WolverineFx.SqlServer`/`.Postgresql` **6.14.1-cw3252**
  pins. Cross-engine repro is green and un-skipped:
  `src/Tests/Integration/db_queue_control_channel_cross_engine.cs` (note `:29-37`). Same-engine and
  endpoint-mode tests also present (`db_queue_control_channel_same_engine.cs`,
  `db_queue_control_channel_endpoint_modes.cs`). `IsDatabaseQueueScheme` +
  `controlListener.BufferedInMemory()` already special-case DB queues.
- **Source-tagging `#3221` — SHIPPED unconditionally** in `WolverineFx 6.14.1-cw469` (no
  `EnableAdvancedTracking` flag). Relevant to the metrics route, not the control channel.
- **Solo heartbeat `#510`** — parked (`wolverine#3188` SoloHeartbeatService); affects the liveness
  dot on single-node/`NullMessageStore` hosts, not control-channel provisioning. Samples here run a
  durable store so heartbeats flow normally.

---

## Per-transport verdicts

### 1. DB-backed queues (Postgres / SQL Server) — ✅ WORKS AS-IS (#531 done)

**Verdict: works as-is.** No upstream change. This is the most production-realistic non-broker
path and it is fully wired and tested.

The monitored side uses a database-backed Wolverine transport as both telemetry and control channel.
The only subtleties are already handled in `AddCritterWatchMonitoring`:
- DB queues reject `Inline` and would otherwise default to `Durable` (coupling delivery to the store
  the #531 reconciler demotes to Ancillary), so CritterWatch pins them to **`BufferedInMemory`** for
  both the telemetry route and the control listener (`WolverineOptionsExtensions.cs:122-124,
  145-148, 194-197`).
- The DB transport hard-requires its own DB as a `Main` store; that collides with CritterWatch's
  durability store. `CritterWatchMainStoreReconciliation.ResolveMainStoreOnConflict` keeps CW's
  store as `Main` and demotes the transport store to `Ancillary` (cross-engine proven:
  `db_queue_control_channel_cross_engine.cs`).

**Monitored-side wiring** (port from `src/DocSamples/TransportChannelsSamples.cs:21-44`):
```csharp
opts.ServiceName = "my-service";
opts.Services.AddMarten(connectionString).IntegrateWithWolverine();   // Polecat+SqlServer for the SQL fleet
opts.UsePostgresqlPersistenceAndTransport(
        connectionString, schema: "myapp", transportSchema: "myapp_cw_control",
        role: MessageStoreRole.Ancillary)
    .AutoProvision();
opts.AddCritterWatchMonitoring(
    critterWatchUri:  new Uri("postgresql://critterwatch"),
    systemControlUri: new Uri("postgresql://my-service-control"));
```
SQL Server flavor: `opts.UseSqlServerPersistenceAndTransport(conn, …, role: MessageStoreRole.Ancillary)`
+ `sqlserver://…` URIs (Polecat fleet → `Fleet.SqlServerQueues`).

**Console side:** listen on the DB queue with `.ListenOnlyAtLeader()` and the CW serializer (the
serializer policy auto-applies on the `critterwatch`-named queue;
`db_queue_control_channel_cross_engine.cs:51` shows `opts.ListenToSqlServerQueue("critterwatch")`).
Reconciliation runs in `AddCritterWatch` automatically.

**Panels:** DLQ + Scheduled panels **fully populated.** Both read the durable `IMessageStore`
(`ScheduledMessages/ScheduledMessageHandler.cs:32,88-123`; DLQ Explorer manages the durable DLQ per
`Internal/NativeDeadLetterForwardingStartupCheck.cs:12-22`). A DB-backed service inherently has a
durable store, and `BufferedInMemory` on the *control* route does not change where the *app's* dead
letters / scheduled jobs land (the app's own `IntegrateWithWolverine()` durable store). This is the
richest fleet for those panels — which is why `04` carries the Incidents group here.

---

### 2. Redis (streams) — ✅ WORKS AS-IS

**Verdict: works as-is.** No upstream change. Redis streams + consumer groups give true
competing-consumer queue semantics, and the listener config is a stock `ListenerConfiguration`
subclass.

Evidence (`~/code/wolverine/src/Transports/Redis/Wolverine.Redis/`):
- **Queue semantics:** `RedisStreamListener` reads via `XREADGROUP … ">"` against a shared consumer
  group (`Internal/RedisStreamListener.cs:284-314`) — each message delivered to exactly one consumer
  in the group. Not broadcast. Combined with `ListenOnlyAtLeader` (only the leader starts the
  listener) this is single-consumer by construction.
- **Auto-provision:** `RedisStreamEndpoint.InitializeAsync` honors `_transport.AutoProvision` and
  calls `StreamCreateConsumerGroupAsync(...)` with idempotent `BUSYGROUP` handling
  (`Internal/RedisStreamEndpoint.cs:177-212`); without AutoProvision a missing stream throws a clear
  error.
- **Custom serializer + leader gate:** `RedisListenerConfiguration : ListenerConfiguration<…>`
  (`Internal/RedisTransportExpression.cs:66`) inherits both `DefaultSerializer(...)` and
  `ListenOnlyAtLeader()`.
- **Native DLQ + native scheduled send** both exist (`{stream}:dead-letter`, `{stream}:scheduled`
  sorted set; `RedisStreamEndpoint.cs:46-60`, `SupportsNativeScheduledSend = true`).

**URI scheme:** `redis://stream/{databaseId}/{streamKey}` (e.g. `redis://stream/0/critterwatch`).
Listen with `ListenToRedisStream(streamKey, consumerGroup)`; publish with `ToRedisStream(streamKey)`.

**Monitored-side wiring** (the sample should use):
```csharp
opts.UseRedis(redisConnectionString);     // or UseRedisUsingNamedConnection for Aspire
opts.Services.AddMarten(pgConn).IntegrateWithWolverine();   // durable store for DLQ/scheduled panels
opts.AddCritterWatchMonitoring(
    critterWatchUri:  new Uri("redis://stream/0/critterwatch"),
    systemControlUri: new Uri("redis://stream/0/redis_service"));
```
The telemetry route is a broker scheme (not local, not a DB queue), so `sendInline` stays true —
acceptable for Redis. The control listener inherits `ListenOnlyAtLeader` automatically.

**Console side:** `opts.ListenToRedisStream("critterwatch", "critterwatch-cw").ListenOnlyAtLeader().UseCritterWatchSerializer();`
plus a publish route to each service's `redis://stream/0/{service}_service` control stream. Confirm
the serializer policy's name-match (`CritterWatchSerializerPolicy.IsTelemetryQueueName`) fires on the
Redis listener's `EndpointName` (it is `critterwatch`), else add the explicit
`.UseCritterWatchSerializer()` (already shown above) — **belt-and-suspenders, recommend explicit.**

**Panels:** DLQ + Scheduled **populated when the service has a durable store** (it should — use
Marten/Postgres as in the wiring above). CritterWatch reads the durable `IMessageStore`, not Redis's
native `{stream}:dead-letter`/`{stream}:scheduled`. If a Redis-only service ran without a durable
store, both panels would be empty (and the `NativeDeadLetterForwardingStartupCheck` info line would
fire). **Recommendation: always pair the Redis transport with a durable Marten/Polecat store in the
sample so the panels light up.**

---

### 3. Kafka (topics) — ⚠️ WORKS, WITH A CAVEAT (no upstream code change strictly required)

**Verdict: works as-is for control + telemetry**, but Kafka has **no queue/DLQ/scheduled
semantics**, and the leader-only guarantee rests on the leader-pinned-listener agent rather than on
broker-level competing consumption. Acceptable for a sample; document the caveat. No upstream change
is *required*, but one **config-quality** improvement is worth an issue.

Evidence (`~/code/wolverine/src/Transports/Kafka/Wolverine.Kafka/`):
- **Custom serializer + leader gate:** `KafkaListenerConfiguration : InteroperableListenerConfiguration<…>`
  → `ListenerConfiguration<…>` (`KafkaListenerConfiguration.cs:10`), so `DefaultSerializer(...)` and
  `ListenOnlyAtLeader()` are inherited.
- **Auto-provision topics:** `KafkaTopic.InitializeAsync` creates the topic when
  `Parent.AutoProvision && !IsExternallyOwned` via `CreateTopicsAsync` (`KafkaTopic.cs:290-302`).
  `.ExternallyOwned()` opt-out exists for restricted ACLs.
- **Leader-only is via the agent, not the broker.** All Kafka consumers on a service default to one
  consumer group `ConsumerConfig.GroupId ??= ServiceName` (`Internals/KafkaTransport.cs:103`). Were
  every node to listen, Kafka would *balance partitions* across them (still ~one node per partition),
  but `ListenOnlyAtLeader` means **only the leader node ever creates the consumer**
  (`LeaderPinnedListenerAgent.StartAsync`), so the control topic is consumed by one node. This is
  sound — the caveat is only that a single-partition control topic is correct here (don't shard it),
  which the single `critterwatch`/`{service}` topic naming already gives us.

**Caveats that map to empty panels / behavior, not bugs:**
- **No native scheduled send:** `InlineKafkaSender.SupportsNativeScheduledSend => false`
  (`Internals/InlineKafkaSender.cs:23`).
- **No queue/native-DLQ that CritterWatch reads** — Kafka has an opt-in native DLQ *topic*
  (`EnableNativeDeadLetterQueue`, default topic `wolverine-dead-letter-queue`,
  `KafkaTransport.cs:62`), but it is a Kafka topic, not the durable `IMessageStore` CritterWatch's
  DLQ Explorer manages.

**Monitored-side wiring** (the sample should use):
```csharp
opts.UseKafka(bootstrapServers).AutoProvision();   // or UseKafkaUsingNamedConnection for Aspire
opts.Services.AddMarten(pgConn).IntegrateWithWolverine();   // durable store for DLQ/scheduled panels
opts.AddCritterWatchMonitoring(
    critterWatchUri:  new Uri("kafka://topic/critterwatch"),      // see URI note below
    systemControlUri: new Uri("kafka://topic/kafka_service"));
```
> **URI note for `03`:** Kafka endpoint URIs are derived by `KafkaTopic.TopicNameForUri`
> (`KafkaTransport.findEndpointByUri`, `KafkaTransport.cs:95-99`). Confirm the exact URI host shape
> `AddCritterWatchMonitoring` must pass (`kafka://topic/{name}` vs `kafka://{name}`) by checking
> `KafkaEndpointUri.cs`/`KafkaTopic.TopicNameForUri` when building the sample — mirror the ASB
> `asb://queue/{name}` gotcha (`#345`). The telemetry route is a non-local, non-DB scheme so
> `sendInline` stays true.

**Console side:** `opts.ListenToKafkaTopic("critterwatch").ListenOnlyAtLeader().UseCritterWatchSerializer();`
+ a publish route to each service's control topic. **Pin the control topic to a single partition**
(via `.Specification(s => s.NumPartitions = 1)` on the listener) so leader handoff doesn't strand
in-flight commands across partitions.

**Panels:** DLQ + Scheduled **populated only via the durable store** (use Marten/Postgres as above).
Kafka contributes nothing to those panels natively; with a durable store the service's own dead
letters/scheduled jobs are visible exactly as on RabbitMQ. Without a durable store: both empty +
native-DLQ info line.

**Proposed Wolverine issue (config-quality, not a blocker):**
> **Title:** "Kafka: first-class single-partition control/command topic helper (`ListenToKafkaCommandTopic`)"
> **Sketch:** a thin helper that creates a topic with `NumPartitions = 1`, a per-service stable
> consumer group, and `ListenOnlyAtLeader()` pre-applied — so command/control topics (CritterWatch's
> use case) don't require operators to remember the single-partition + leader-pin + dedicated-group
> incantation. Purely ergonomic; the raw capability already exists.

---

### 4. GCP Pub/Sub (topic + subscription) — ❌ NEEDS UPSTREAM WOLVERINE CHANGE

**Verdict: needs an upstream Wolverine change.** The leader-only guarantee is **broken by per-node
subscription naming.** Everything else (auto-provision, custom serializer, `ListenOnlyAtLeader`
inheritance, native DLQ) is present.

Evidence (`~/code/wolverine/src/Transports/GCP/Wolverine.Pubsub/`):
- **Per-node subscription naming is the gap.** Each node mutates the subscription name with its
  assigned node number:
  ```csharp
  // PubsubEndpoint.cs:135-136
  Server.Subscription.Name =
      Server.Subscription.Name.WithAssignedNodeNumber(_transport.AssignedNodeNumber);
  // SubscriptionNameExtensions.cs:10-11  →  "{subId}.{Abs(nodeNumber)}"
  ```
  In Pub/Sub, **distinct subscriptions on one topic each receive a full copy** of every published
  message (fan-out), while consumers on the *same* subscription compete. So with
  `ListenToPubsubTopic("critterwatch-control")`:
  - every node provisions its own `critterwatch-control.{n}` subscription (provisioning runs on all
    nodes in `InitializeAsync`, independent of whether the listener starts —
    `PubsubEndpoint.cs:269-299`);
  - the CritterWatch console publishes to the **topic**, fanning a copy into *every* node's
    subscription;
  - `ListenOnlyAtLeader` correctly starts the listener only on the leader, so commands are handled
    once — **but** the leader's subscription id is `…{leaderNodeNumber}`. On a leader election the
    new leader listens on a *different* subscription that may have missed messages delivered while it
    wasn't leader, and every follower subscription accumulates an unacked backlog.
- **What already works:** `PubsubTopicListenerConfiguration` derives from
  `InteroperableListenerConfiguration → ListenerConfiguration` so `DefaultSerializer(...)` and
  `ListenOnlyAtLeader()` are inherited; `AutoProvision` auto-creates both topic and subscription
  (`PubsubEndpoint.cs:102-166, 282-284`); native dead-lettering via `DeadLetterPolicy` exists
  (`.ConfigureDeadLettering()`). Native scheduled send is **not** supported
  (`InlinePubsubSender.cs:21` `SupportsNativeScheduledSend => false`).

**Precise gap:** there is no way to make all nodes of a service share ONE subscription so that a
single leader-pinned listener has competing-consumer semantics. The node-number suffix is
unconditional for non-dead-letter subscriptions.

**Proposed Wolverine issue:**
> **Title:** "GCP Pub/Sub: support a shared (non-per-node) subscription for leader-pinned / competing-consumer listeners"
> **Fix sketch (smallest):** when the endpoint's `ListenerScope == ListenerScope.PinnedToLeader`,
> skip the `WithAssignedNodeNumber(...)` suffix in `PubsubEndpoint` (the conditional the spike
> identified at `PubsubEndpoint.cs:133-137`) so all nodes resolve the *same* subscription id; the
> leader-pinned agent already guarantees only one node consumes it. **Alternative (more explicit):**
> add `ListenToPubsubSharedSubscription(topic, subscriptionName)` (or a
> `.SharedSubscription()`/`.NotPartitionedByNode()` toggle on
> `PubsubTopicListenerConfiguration`) that opts the subscription out of node-number suffixing. Either
> lights up a leader-owned control channel; the toggle form is safer (doesn't silently change
> behavior of existing leader-pinned Pub/Sub listeners).

**Until that lands, options for the GCP sample (`03`):**
1. **Ship `Fleet.GooglePubSub` last / mark blocked** on the upstream issue (cleanest — matches the
   spike's gating intent).
2. **Workaround without upstream code:** pre-create a single shared subscription out-of-band (Aspire
   emulator init / `gcloud`) and attach via `IsExistingSubscription` (`PubsubEndpoint.cs:271`) so
   Wolverine doesn't suffix it, then `ListenOnlyAtLeader()`. Verify
   `IsExistingSubscription`/`ListenToPubsubSubscription`-style entry exists before committing to this
   — if Wolverine only exposes topic-addressed listening, option 1 is required.

**Monitored-side wiring (target shape, assumes the upstream fix or option-2 shared sub):**
```csharp
opts.UsePubsub(projectId).AutoProvision();
opts.Services.AddMarten(pgConn).IntegrateWithWolverine();
opts.AddCritterWatchMonitoring(
    critterWatchUri:  new Uri("pubsub://{projectId}/critterwatch"),
    systemControlUri: new Uri("pubsub://{projectId}/pubsub_service"));   // must resolve to a SHARED sub
```

**Panels:** DLQ + Scheduled **populated only via the durable store** (Pub/Sub has native DLQ topics
but no native scheduling and nothing CritterWatch's durable-store panels read). Pair with
Marten/Postgres as in every other fleet.

---

## Summary table

| Transport | Control channel verdict | Upstream needed | DLQ panel | Scheduled panel |
|-----------|-------------------------|-----------------|-----------|-----------------|
| **DB queues (PG/SQL)** | ✅ works as-is (#531 done, `-cw3252` pins) | No | Populated (durable store) | Populated (durable store) |
| **Redis (streams)** | ✅ works as-is | No | Populated *iff* durable store paired | Populated *iff* durable store paired |
| **Kafka (topics)** | ⚠️ works; leader gate via agent, no native queue/DLQ/schedule | No (optional ergonomic helper) | Populated *iff* durable store paired | Populated *iff* durable store paired |
| **GCP Pub/Sub** | ❌ leader-only broken by per-node subscription naming | **Yes** — shared-subscription option | Populated *iff* durable store paired | Populated *iff* durable store paired |

**Universal rule for `03`/`04`:** the DLQ and Scheduled panels read CritterWatch's view of the
**durable `IMessageStore`**, not any broker-native DLQ/scheduler
(`ScheduledMessages/ScheduledMessageHandler.cs:32`; `Internal/NativeDeadLetterForwardingStartupCheck.cs:12-22`).
So every non-DB fleet sample MUST pair its transport with a durable Marten (or Polecat) store for
those panels to light up — the transport choice governs only the control/telemetry path. DB-queue
fleets get both for free.

**Build-order implication:** `Fleet.PostgresqlQueues`/`Fleet.SqlServerQueues` (04, ✅), `Fleet.Redis`
(03, ✅), and `Fleet.Kafka` (03, ⚠️ document caveat + single-partition control topic) are unblocked.
`Fleet.GooglePubSub` (03) is **blocked on the upstream Pub/Sub shared-subscription change** — file the
issue, then build last or via the existing-subscription workaround.
