# 09 — Upstream spike: control-channel parity (Kafka / Redis / GCP / DB queues)

**Read `plans/README.md` first. This is a READ-ONLY investigation + findings doc, not a sample build.**
It gates `03-fleet-other-transports.md` (Kafka/Redis/GCP) and `04-fleet-db-queues.md`. Run it in parallel
with the flagship.

## Question to answer
CritterWatch's monitoring rides a **leader-only control queue using the CritterWatch serializer** (the
pattern proven for RabbitMQ / Azure Service Bus / Amazon SQS — see the `#356` ASB work and
`src/BffHost/Composition`). Does that pattern provision and round-trip on:
- **Kafka** (topics, no queue semantics, no DLQ)
- **Redis** (streams)
- **GCP Pub/Sub** (topic + subscription)
- **DB-backed queues** (Postgres / SQL Server) — likely already done (#531, `-cw3252` pins); confirm.

> Any actual CritterWatch/Wolverine *code changes* that fall out of this spike are implemented later, in
> the dedicated worktree `~/code/_cw_worktrees/samples-support` (see README "CritterWatch-side changes"),
> never in the main working copy. This spike itself is read-only and only writes `09-FINDINGS.md`.

## How to investigate (`~/code/CritterWatch` + `~/code/wolverine`)
- Read how the control channel is established today: grep `ControlUri`, control-queue setup, the
  CritterWatch serializer registration, and the leader-only gating in
  `Wolverine.CritterWatch/**` and `src/BffHost/Composition/**`.
- For each transport, read the Wolverine transport source under `~/code/wolverine/src/Transports/{Kafka,Redis,GCP,...}`
  and its docs to see whether a queue-like, leader-owned, auto-provisioned control endpoint with a custom
  serializer is expressible. Note per-transport provisioning quirks (Kafka topic auto-create, Redis stream
  groups, Pub/Sub subscription creation).
- Check existing CritterWatch tests/samples for any non-Rabbit/ASB/SQS control-channel usage.

## Deliverable: `plans/09-FINDINGS.md`
For each of the 4 transports:
- **Verdict:** works as-is / needs config / needs upstream Wolverine change.
- If upstream change needed: the precise gap (API that doesn't exist, provisioning that fails) and a
  proposed Wolverine issue title + sketch (e.g. a per-transport "CritterWatch control endpoint" helper).
- The exact monitored-side + console-side wiring a sample should use (so `03`/`04` can copy it).
- Which CritterWatch panels will be empty on that transport (DLQ/scheduled) and why.

Also note any Wolverine work already known/landed (per CritterWatch memory: source-tagging #3221 shipped;
#531 cross-engine DB-queue resolved on `-cw3252`; SoloHeartbeat #510). Cross-reference so we don't
re-investigate solved items.
