# ShipmentTracking specs — a Bobcat calibration run

This project exists to answer one question: **what does Bobcat need in order to describe a
Wolverine application that is not event sourced?** ShipmentTracking was chosen for breadth — Polecat
documents, declarative persistence, cascading messages, a delivery saga, an outbox, `IRevisioned`
optimistic concurrency — and deliberately *not* for fit.

It found six issues ([bobcat#269–#274](https://github.com/JasperFx/bobcat/issues/270)), all fixed in
**Bobcat 0.18.0**. This project is now written entirely in shipped vocabulary.

## State: 2 of 3 scenarios pass

| Scenario | |
|---|---|
| Booking a shipment stores it and cascades | **passes** |
| Cancelling a booked shipment marks it cancelled | **passes** |
| The booking endpoint accepts and does not handle inline | fails — see below |

The remaining failure is **this suite's problem, not Bobcat's**. The per-scenario reset
(`ResetAllPolecatDataAsync`) clears the document store, but messages the previous scenario's
cascade already put on RabbitMQ survive it. They then land inside the *next* scenario's tracked
session and fail with `RequiredDataMissingException: Unknown Shipment …` naming the **previous**
scenario's id. Reproducible, not flaky.

Bobcat 0.18.0 is what makes that diagnosable at all: the failure message distinguishes "the HTTP
call completed but its cascade did not" from "no act ran", and quotes the exception
([#271](https://github.com/JasperFx/bobcat/issues/271)).

## What 0.18.0 removed from this project

Every workaround the first pass needed is gone:

- **`DocumentGrammars.cs` — deleted.** 0.18.0 ships the document lane with a `{document}` capture
  ([#270](https://github.com/JasperFx/bobcat/issues/270)); the shipped steps are textually identical
  to the ones sketched here.
- **The hand-rolled record binder — deleted.** `RecordBuilding` is public
  ([#272](https://github.com/JasperFx/bobcat/issues/272)).
- **`[FixtureTitle]` — deleted.** Feature titles are spaced (`Feature: Booking Shipments`) so the
  camel-hump convention binds them to `BookingShipments`, which is what
  [#273](https://github.com/JasperFx/bobcat/issues/273) made the diagnostic explain.
- **The explicit `UseContentRoot` — deleted**, in favour of `AlbaResource<Program>`, which resolves
  the root itself. This repository's solution file sits in a directory named after the project,
  which is precisely the shape that misleads `WebApplicationFactory`'s fallback; 0.18.0 now
  diagnoses that instead of printing a bare path
  ([#274](https://github.com/JasperFx/bobcat/issues/274)).

## Running it

SQL Server 2025 on `localhost,1433` and RabbitMQ on 5672:

```bash
docker compose up -d          # from ShipmentTracking/
dotnet run --project Specs
```

The specs use their own database (`ShipmentTracking_Specs`), separate from the xUnit suite's. The
application's own 33-test xUnit suite covers this behaviour and passes; nothing here is evidence
that ShipmentTracking is broken.
