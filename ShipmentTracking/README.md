# ShipmentTracking

A Wolverine service converted from NServiceBus. Phases 1–2 of a migration walkthrough:
NServiceBus → Wolverine → Wolverine.HTTP → Polecat → integration tests → Aspire + CritterWatch.

The NServiceBus starting point is **not** in this repository. NServiceBus is RPL-1.5
licensed and this repo is MIT, so the "before" state was written from scratch against
NServiceBus' public API and lives outside the tree; the write-up quotes it inline.
See the licensing advisories in `../sample-projects.md`.

## What the migration actually decided

The NServiceBus version had **one endpoint**, so it had one concurrency setting and
one recoverability policy for three workloads that want different things. That is not
a criticism of NServiceBus — it is the shape of the model. Nothing asks you which of
the three you meant, because there is only one answer available.

Wolverine asks per listener, and the three answers differ for three different reasons:

| Endpoint | Mode | Decided by |
|---|---|---|
| `carrier-events` | `NativeAck`, globally partitioned | **Throughput cost** — Durable's per-message inbox writes are the ceiling |
| `shipment-commands` | `Durable` | **Delivery guarantee** — the outbox has to carry follow-on events |
| `label-generation` | `Durable`, 4 parallel | **Handler duration** — the axis nobody asks about |

`label-generation` is the interesting one. It looks exactly like `carrier-events` —
same broker, same "we need throughput" complaint — and it is the one endpoint that
must **not** be on `NativeAck`. The carrier label API takes 30–90 seconds, and under
`NativeAck` the broker's clock runs for the whole handler: the lease expires, the
broker redelivers, and the duplicate runs *concurrently with the original*.

## Ordering, and the trap in it

"Scans for one shipment must be processed in order" is not solved by the mode. A
single `carrier-events` queue with competing consumers across nodes has no ordering
guarantee at all, and `PartitionProcessingByGroupId` on the listener would only order
each node's own work — two nodes would still process the same shipment at once.

So the topology is global: `MessagePartitioning.GlobalPartitioned` with sharded
RabbitMQ queues, grouping inferred from `ShipmentId`. One shipment always lands on the
same shard, and only then does ordering hold cluster-wide.

`NativeAck` also means redelivery is expected by design, so `CarrierScanHandler` is
idempotent by construction — the scan update is guarded on being newer than the one
already recorded, so a duplicate is a no-op rather than a regression.

## Conversion notes

| NServiceBus | Here |
|---|---|
| `IHandleMessages<T>` classes | static handler classes; `Handle` found by convention |
| `context.Publish` / `context.Send` | cascading returns — `OutgoingMessages`, or the message itself |
| `Saga<TData>` + `ContainSagaData` | `Saga` base class; state lives on the saga |
| `ConfigureHowToFindSaga` mapper | `[SagaIdentity]` on message properties |
| `IAmStartedByMessages<T>` / `IHandleTimeouts<T>` | `Start(T)` / an ordinary `Handle(T)` — a timeout is just a scheduled message |
| `RequestTimeout<T>(context, delay)` | `DeliverySlaExpired : TimeoutMessage(5.Days())` — the delay is on the message type, so `Start` just returns it |
| `MarkAsComplete()` | `MarkCompleted()` |
| `Behavior<IIncomingLogicalMessageContext>` | **deleted** — see below |
| `IMessageSession` outside handlers | `IMessageBus` |
| `context.MessageHeaders` | `Envelope.Headers` |
| `UsePersistence<SqlPersistence>()` + `EnableOutbox()` | `PersistMessagesWithSqlServer(...)` |
| endpoint-wide `Recoverability()` | `OnException<T>()` policies, per exception type |
| `EnableInstallers()` | `RunJasperFxCommands(args)` |

**The most interesting conversion was a deletion.** The NServiceBus pipeline behavior
pulled a correlation id off the headers and pushed it, with the message type, into a
logging scope around `await next()`.

Wolverine could express that — `Before` can return a value later methods receive, and
`Finally` runs inside a `try/finally`, so a scope opened in `Before` can be disposed in
`Finally` and cover the failure path. But it should not, because the behavior existed
to compensate for something Wolverine already does. Its OpenTelemetry spans carry
`messaging.conversation_id` (the correlation id), `messaging.message_type`,
`messaging.message_id`, `message.handler` and `handler.type` — a superset of what the
behavior was assembling by hand, on a span rather than a log scope.

So the conversion is: delete it, register the `Wolverine` `ActivitySource`, and read the
trace. **Not every behavior wants an equivalent** — some are scaffolding around a gap
the new framework does not have.

## Phase 2 — Minimal API to Wolverine.HTTP

The endpoints were minimal API lambdas that injected `IMessageBus` and called it
explicitly. They are now endpoint methods discovered by `MapWolverineEndpoints()`.

**The explicit bus call is gone the same way `context.Publish` went in phase 1.** A
command is the second element of a tuple return, so Wolverine sends it through the
outbox after the response is written — the same cascading shape the message handlers
already use.

### What a client sees: nothing changed

Every route, verb, status code and response body is identical, including 202 on all
three command routes and the `Location` header on `POST /shipments` only.

**Wolverine ships `AcceptResponse` for 202**, the sibling of `CreationResponse`, in the
same file and on the same `IHttpAware` seam. `ShipmentAccepted` derives from it, so the
status code, the `Location` header and the OpenAPI metadata all come for free:

```csharp
public record ShipmentAccepted(Guid ShipmentId) : AcceptResponse($"/shipments/{ShipmentId}");
```

The two routes that return 202 with **no** body are the exception. `AcceptResponse`
requires a `Url` and always stamps `Location`, and those routes never had one — the
minimal API returned a bare `Results.Accepted()`. They use `TypedResults.Accepted((string?)null)`
instead, which is still a concrete type: `Accepted` implements `IEndpointMetadataProvider`
and Wolverine calls `PopulateMetadata` on any return type that does
(`HttpChain.EndpointBuilder.tryApplyAsEndpointMetadataProvider`), so 202 still reaches
the OpenAPI document. This is not the opaque `IResult` case the docs warn about.

> A wrong turn worth recording, because it is easy to repeat: the first cut of this
> conversion hand-rolled an `AcceptResponse` against `IHttpAware`, having grepped for
> `class AcceptResponse` and found nothing. It is a `record`. Forty lines reimplementing
> a type that already shipped. **Absence of a grep hit is not absence of an API** — and
> in `Wolverine.Http`, response types live together in `IHttpAware.cs`.

### What did change: the OpenAPI document got better

The minimal API returned `Results.Accepted(...)` — the untyped helper, which tells the
generator nothing. The converted endpoints return `Accepted<T>`, so 202 and the response
schema are both documented, and `GET /shipments/{id}` gets 200 and 404 from its nullable
return with no attribute at all. Additive: no client behaviour changes, but a generated
client would now be correct where it previously was not.

### A saga method returns the cascade, not itself

`Start` sets the saga's state and returns **only** what should happen next:

```csharp
public DeliverySlaExpired Start(ShipmentBooked booked)
{
    Id = booked.ShipmentId;
    BookedAt = booked.BookedAt;
    return new DeliverySlaExpired(booked.ShipmentId);
}
```

Returning `(this, cascade)` compiles and appears to work, which is what makes it worth
naming: that tuple shape belongs to an **immutable** saga, or to a `static Start` that
*creates* the instance. On a mutable saga it reads as though the state were being
returned when it is really being mutated in place.

The delay is not here either. `DeliverySlaExpired` subclasses `TimeoutMessage(5.Days())`,
so every instance is scheduled five days out wherever it is returned, and the saga method
stays a pure function naming an outcome rather than scheduling one.

## Still carried over

`Data/ShipmentRepository.cs` is unchanged Dapper, and it still opens its own connection
outside Wolverine's outbox transaction. Phase 2 replaces it with Polecat, which is where
that gets fixed.

## Running it

Needs SQL Server and RabbitMQ — `docker-compose.yml` at the repository root.

```bash
dotnet run --project ShipmentTracking
```
