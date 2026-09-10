# ShipmentTracking specs — a Bobcat calibration run, not a finished suite

This project exists to answer one question: **what does Bobcat need in order to describe a
Wolverine application that is not event sourced?** ShipmentTracking was chosen for breadth — Polecat
documents, declarative persistence, cascading messages, a delivery saga, an outbox, `IRevisioned`
optimistic concurrency — and deliberately *not* for fit.

## State: three specs discover and execute, none pass

The host really comes up — real Wolverine, real RabbitMQ, real Polecat on SQL Server 2025, ~11s —
and the scenarios really run. Every failure is a filed Bobcat issue rather than a defect in the
application or a mistake in the spec:

| Failing scenario | Issue |
|---|---|
| Booking a shipment stores it and cascades | [#270](https://github.com/JasperFx/bobcat/issues/270) — no shipped vocabulary for documents |
| The booking endpoint accepts and does not handle inline | [#271](https://github.com/JasperFx/bobcat/issues/271) — the HTTP act and `Then {message} is sent` do not compose |
| Cancelling a booked shipment marks it cancelled | [#270](https://github.com/JasperFx/bobcat/issues/270) |

The application's own xUnit suite covers all of this and passes 33/33. Nothing here is evidence
that ShipmentTracking is broken.

## What is a workaround and should be deleted

- **`DocumentGrammars.cs`** — the whole module. Four of ten shipped steps apply to a document
  application; this supplies arrange-a-document and assert-a-document. It is the working sketch
  attached to [#270](https://github.com/JasperFx/bobcat/issues/270).
- **`bindRow` inside it** — a poorer copy of Bobcat's `RecordBuilding`, which is `internal`
  ([#272](https://github.com/JasperFx/bobcat/issues/272)).
- **`loadAsync` inside it** — reflection, because `LoadAsync` is generic-only and a `{type}`-captured
  step has only a `Type`.
- **`UseContentRoot` in `SuiteConfiguration`** — the Alba resource resolved a doubled content root
  ([#274](https://github.com/JasperFx/bobcat/issues/274)).
- **`[FixtureTitle]` on the fixtures** — the convention naming did not match here; see the note on
  [#273](https://github.com/JasperFx/bobcat/issues/273).

## Running it

SQL Server 2025 on `localhost,1433` and RabbitMQ on 5672:

```bash
docker compose up -d          # from ShipmentTracking/
dotnet run --project Specs
```

The specs use their own database (`ShipmentTracking_Specs`), separate from the xUnit suite's.
