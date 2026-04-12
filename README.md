# CritterStackSamples

Sample projects using the "Critter Stack" tools ([Marten](https://martendb.io) and [Wolverine](https://wolverinefx.net)) and related [JasperFx](https://github.com/jasperfx) projects.

Most of these samples require PostgreSQL. Use [Docker Desktop](https://www.docker.com/products/docker-desktop/) or a local PostgreSQL instance.

## Samples

| Sample | Original Project | Description | Patterns |
|--------|-----------------|-------------|----------|
| [CqrsMinimalApi](CqrsMinimalApi/) | [matjazbravc/CQRS.MinimalAPI.Demo](https://github.com/matjazbravc/CQRS.MinimalAPI.Demo) | Student CRUD — simplest MediatR → Wolverine port | Wolverine.HTTP, Marten documents, `[Entity]`, Alba tests |
| [CleanArchitectureTodos](CleanArchitectureTodos/) | [jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture) | Todo lists — Clean Architecture unraveling (67 files → 11) | FluentValidation middleware, `ValidateAsync`, one-file-per-request layout |
| [OutboxDemo](OutboxDemo/) | [MassTransit/Sample-Outbox](https://github.com/MassTransit/Sample-Outbox) | Registration workflow with transactional outbox and Saga | Marten outbox, Wolverine Saga, cascading messages, `Results.NoContent()` |
| [EcommerceMicroservices](EcommerceMicroservices/) | [aspnetrun/run-aspnetcore-microservices](https://github.com/aspnetrun/run-aspnetcore-microservices) | E-commerce with 4 services communicating via RabbitMQ | Wolverine RabbitMQ transport, per-service databases, `[Entity]` |
| [EcommerceModularMonolith](EcommerceModularMonolith/) | Same as above | Same domain collapsed into one app with durable local queues | Schema-per-module, durable local queues, same handler code as microservices |
| [MeetingGroupMonolith](MeetingGroupMonolith/) | [kgrzybek/modular-monolith-with-ddd](https://github.com/kgrzybek/modular-monolith-with-ddd) | Meeting group scheduling — 5 modules with event sourcing | Marten event store (Payments), durable local queues, inter-module events |
| [PaymentsMonolith](PaymentsMonolith/) | [devmentors/Inflow](https://github.com/devmentors/Inflow) | Virtual payments — 4 modules (Users, Customers, Wallets, Payments) | Schema-per-module, cascading events across modules, `ValidateAsync` |
| [BookingMonolith](BookingMonolith/) | [meysamhadeli/booking-modular-monolith](https://github.com/meysamhadeli/booking-modular-monolith) | Travel booking — replaces EventStoreDB + MongoDB with Marten | Marten event store, inline snapshots, multiple `[Entity]` batch loading |
| [BankAccountES](BankAccountES/) | Inspired by [andreschaffer/event-sourcing-cqrs-examples](https://github.com/andreschaffer/event-sourcing-cqrs-examples) | Bank accounts — pure Marten event sourcing from scratch | `[AggregateHandler]`, `[WriteAggregate]`, inline projections, `Validate` against aggregate state |
| [MoreSpeakers](MoreSpeakers/) | [cwoodruff/morespeakers-com](https://github.com/cwoodruff/morespeakers-com) | Speaker mentorship platform — Marten as document DB | Nested collections, multiple `[Entity]` batch queries, mentorship lifecycle |

## Common Patterns Across Samples

- **`IntegrateWithWolverine()` + `AutoApplyTransactions()`** — canonical Marten + Wolverine setup in every sample
- **`AddWolverineHttp()`** — required for Wolverine.HTTP endpoints
- **`[Entity]`** — declarative entity loading (Marten documents, event-sourced snapshots)
- **`[WriteAggregate]` + `IEventStream<T>`** — event-sourced aggregate mutations
- **`ValidateAsync` / `Validate`** — sad-path validation separated from happy-path handlers
- **`Results.NoContent()`** — preferred over `[EmptyResponse]` for 204 responses with cascading messages
- **FluentValidation** — `UseFluentValidationProblemDetailMiddleware()` in `MapWolverineEndpoints()`
- **Alba + Shouldly** — integration tests with `CleanAllMartenDataAsync()` for test isolation

## Running

Each sample has its own `.sln` file and `Tests/` subfolder. Requires PostgreSQL:

```bash
cd BankAccountES
dotnet test
```
