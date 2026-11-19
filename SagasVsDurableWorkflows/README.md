# Sagas vs Durable Workflows

Eight sample workflows published for Temporal, Restate, Durable Functions and Dapr Workflow, rebuilt as
ordinary Wolverine sagas on Marten. The point was to see, scenario by scenario, where a saga is as good as a
"workflow as code" tool, where it's clumsier, and where something is really missing.
[EVALUATION.md](EVALUATION.md) has the findings, and its last section says what changed once the saga features it
asked for shipped in Wolverine 7.0.0-alpha.3.

Every domain was reimplemented from the original's description. No code was copied.

| Folder | Original | Tool | What it shows |
|--------|----------|------|---------------|
| [MoneyTransfer](SagaWorkflows/MoneyTransfer/) | [samples-dotnet/src/Saga](https://github.com/temporalio/samples-dotnet/tree/main/src/Saga) (MIT) | Temporal | Withdraw, deposit, a failing last step, undo newest first |
| [TripBooking](SagaWorkflows/TripBooking/) | [patterns-use-cases/src/sagas](https://github.com/restatedev/examples/tree/main/typescript/patterns-use-cases/src/sagas) (MIT) | Restate | Flight, car, hotel. A refusal cancels in reverse order, each cancellation confirmed before the next |
| [OrderApproval](SagaWorkflows/OrderApproval/) | [examples/Workflow/WorkflowConsoleApp](https://github.com/dapr/dotnet-sdk/tree/master/examples/Workflow/WorkflowConsoleApp) (Apache 2.0) | Dapr Workflow | Manager approval with a timeout, signals, a status query and a history over HTTP |
| [Subscriptions](SagaWorkflows/Subscriptions/) | [subscription-workflow-project-template-typescript](https://github.com/temporalio/subscription-workflow-project-template-typescript) (no license file; domain only) | Temporal | Trial, then billing every period; cancel and change the plan while it runs; query it, also after it ends |
| [FanOutFanIn](SagaWorkflows/FanOutFanIn/) | [Fan-out/fan-in (cloud backup)](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-fan-in-fan-out), also Restate's [parallelizework](https://github.com/restatedev/examples/tree/main/typescript/patterns-use-cases/src/parallelizework) | Durable Functions, Restate | Copy every file in parallel, add up the bytes |
| [ChildWorkflows](SagaWorkflows/ChildWorkflows/) | [Sub-orchestrations](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-sub-orchestrations) | Durable Functions | A rollout saga starts a provisioning saga per device and waits for them all |
| [Polling](SagaWorkflows/Polling/) | [samples-dotnet/src/Polling](https://github.com/temporalio/samples-dotnet/tree/main/src/Polling) (MIT), Durable Functions' [monitor pattern](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-monitor) | Temporal, Durable Functions | Ask a slow system until the job is done, backing off, under an overall deadline |
| [AgentLoop](SagaWorkflows/AgentLoop/) | [temporal-ai-agent](https://github.com/temporal-community/temporal-ai-agent) (MIT), Restate's [ai-examples](https://github.com/restatedev/ai-examples) tour of agents | Temporal, Restate | An LLM tool loop: parallel tool calls, a person approves the risky tool, a turn limit, a failed tool fed back to the model |

The "activities" (the bank, the airline, the LLM...) are ordinary Wolverine handlers over in-memory fakes, in the
same folder as each saga. In a real system they would often be in another service; nothing in the sagas would
change.

## Which Wolverine 7 saga features each sample uses

| Sample | Features |
|---|---|
| MoneyTransfer: `Transfer` | `CompensatingSaga` status and compensation log; the undo stack (`PushUndo`, `ConfirmUndo`, `UndoMessages`); one `Compensate(ITransferStep, ExceptionInfo)` for every step (an interface covers each concrete message the saga sends, #5672), with an exact-type `Compensate(Deposit, ...)` beside it; `[TerminalFailure]`; `Fault<T>` back to the saga; dead letters linked to the saga id |
| MoneyTransfer: `EventSourcedTransfer` | `EventSourcedSaga` with `Apply` methods on the same messages; `SagaCompleted`; the undo stack and the compensation as Wolverine's own events on the stream; history and time travel (`ISagaHistoryDiagnostics`: a page of the stream, the saga at a version or at a time) |
| TripBooking: `Trip` | `PushUndo(..., confirmedBy:)` before each step; `UndoSequentially(completeWhenDone: true)`; `UndoMessagesSince("car")`; `RemoteFailure<BookingRefusedException>` matching a subclass through the exception's type hierarchy; `[RetainWhenCompleted]` and its sweep |
| TripBooking: `EventSourcedTrip` | `EventSourcedSaga` written with `Evolve()`; `PushedUndo(...)` from `Start`; `[Compensates]` naming the steps a catch-all `Compensate(object, ...)` covers; `[ArchiveWhenCompleted]` |
| OrderApproval: `Order` | `[DiscardIfSagaMissing]` on a `Handle` method; `[RecordSagaHistory]` and a `GET /orders/{id}/history` endpoint; `[RetainWhenCompleted]`; compensation of a sent step |
| Subscriptions: `Subscription` | `[ReplacesEarlierTimeouts]` instead of a stale-period guard; `[SagaMissing(SagaMissingBehavior.Discard)]` instead of a `NotFound` per signal; `[RetainWhenCompleted]` |
| FanOutFanIn: `Backup` | Global partitioning (`GlobalPartitioned` on sharded PostgreSQL queues) with `UseInferredMessageGrouping()` for every `IFanInReply`; `Handle(Fault<BackupFile>)` for a branch that fails |
| ChildWorkflows | The same fan-in partitioning for the children's results; `[DiscardIfSagaMissing]` on a message type |
| Polling: `ExportWatch` | An overall deadline beside the polls, tested by letting time pass (`PlayScheduledMessagesAsync(advanceBy:)`, Bobcat's `WhenTimePasses`); `Compensate(PollExport, ReportingUnavailableException)` for the saga's own handler, its dead letter tagged `compensated` |
| AgentLoop: `AgentRun` | The fan-in partitioning for tool results; `Saga.LastModified` indexed in Marten and a `GET /agents/idle` endpoint over `ISagaIdleDiagnostics` |
| `Program.cs` | `opts.Policies.WhenSagaIsMissing(...)`, the partitioning topology, the error policies, and the switch between the two saga styles |

Every saga can be drawn with Wolverine's `saga-flow` command:

```bash
cd SagaWorkflows
dotnet run -- saga-flow                       # every saga, as Markdown with Mermaid diagrams
dotnet run -- saga-flow ExportWatch --mermaid-only
Sagas__EventSourced=true dotnet run -- saga-flow EventSourcedTransfer --mermaid-only
```

## Running it

You need the .NET 10 SDK and PostgreSQL.

**PostgreSQL.** The connection string is `ConnectionStrings:Marten` in `SagaWorkflows/appsettings.json`. It points at
`localhost:5442`, database `saga_eval`, user `postgres`/`postgres`, because that is where this was built. Point it at
any PostgreSQL by overriding it, for example the repository's own `docker compose up -d` Postgres:

```bash
export ConnectionStrings__Marten="Host=localhost;Port=5433;Database=saga_eval;Username=postgres;Password=postgres"
```

Create the database first; Marten creates its schema on the first run. The application uses the `saga_workflows`
schema, the tests the same, and the specs `saga_specs` and `saga_specs_es`.

**Two saga styles.** The money transfer and the trip are written twice, on the same messages: as document sagas and
as event-sourced sagas. `Sagas:EventSourced=true` (or `Sagas__EventSourced=true` in the environment) runs the
event-sourced ones; use `Marten:Schema` to keep the two apart.

**Wolverine packages.** Everything is on Wolverine `7.0.0-alpha.3` from nuget.org (Marten 10.0.0-alpha.5, JasperFx 3.8.0).

**Specs.** `SagaWorkflows.Specs` is one [Bobcat](https://wolverinefx.net/guide/testing-with-bobcat) specification
project for all eight samples: ordinary xUnit v3 test methods that render as Given / When / Then, one feature per
sample, in a folder named after it. One host runs the document sagas and another the event-sourced ones.

```bash
dotnet test --project SagaWorkflows.Specs
dotnet test --project SagaWorkflows.Specs --filter-namespace "*TripBooking"
dotnet test --project SagaWorkflows.Specs --filter-class "*watching_an_export"
```

47 specs, about ten seconds. To see the rendered specifications rather than a pass count, start `bobcat ui` first, or
set `BOBCAT_VERBOSE_REPORTS=1`.

**Tests.** `SagaWorkflows.Tests` holds the evaluation's original 32 tests (tracked sessions, Alba scenarios over
the HTTP endpoints, and a few pure-function tests), updated for alpha.3.

```bash
dotnet test --project SagaWorkflows.Tests
```

**The deck.** [slides/](slides/) is the Slidev deck for the talk these samples are demoed in. `npm install` and
`npm run dev` there.

## Things to look at

- `Program.cs` is the whole infrastructure: Marten, durable local queues, the global partitioning topology for
  fan-in, and two error policies.
- Every saga starts with a comment naming the original it was rebuilt from.
- Each saga file holds the saga; the shared messages of the money transfer and the trip are in `Messages.cs`. The
  tool's code puts the steps in one method; here they are `Start`, `Handle` and `Compensate` methods on one class.
