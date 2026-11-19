# Sagas vs Durable Workflows: an evaluation

The question for [wolverine#5405](https://github.com/JasperFx/wolverine/issues/5405): now that Wolverine sagas can
compensate (`CompensatingSaga`, `Compensate` methods, `RemoteFailure<T>`, `[TerminalFailure]`, durable `Fault<T>`,
PR #5659), does a Temporal / Restate style "workflow as code" feature buy us anything real? To find out, I took eight
samples the workflow tools publish for themselves and rebuilt each one as plain Wolverine sagas on Marten, with tests.
The code is in this folder; how to run it is in [README.md](README.md).

Built against Wolverine `7.0.0-local.1`, packed from commit `5e696ddc5` (the #5659 branch). 32 tests, all green, three
runs in a row.

## How the scenarios were picked

I looked at the published samples for Temporal (samples-dotnet, samples-go/java/typescript, the money transfer and
subscription templates, temporal-ai-agent), Restate (examples, ai-examples), Durable Functions / Durable Task (the
application pattern docs), Dapr Workflow (dotnet-sdk examples), DBOS (demo apps), Inngest, AWS Step Functions and the
new Lambda durable functions, plus MassTransit Courier and the NServiceBus saga samples for contrast. Nearly
everything falls into eight shapes, and most of the shapes appear in several tools:

| Shape | Appears as |
|---|---|
| Compensating transaction | Temporal Saga / money transfer, Temporal compensating-transactions, MassTransit Courier |
| LIFO undo of reservations | Restate trip booking, Temporal trip booking tutorial, Courier routing slips |
| Wait for a person, with a timeout | Dapr order processing, Durable Functions human interaction, Temporal expense, Inngest `waitForEvent`, Step Functions callbacks, DBOS checkout |
| Long-lived recurring work | Temporal subscription, Restate payment reminders, Temporal `SleepForDays` |
| Fan-out / fan-in | Durable Functions cloud backup, Restate parallelizework, Dapr fan-out, Step Functions Map |
| Child workflows | Durable Functions sub-orchestrations, Temporal child workflows |
| Poll until done | Temporal Polling, Durable Functions monitor, Dapr monitor |
| Agent loop | temporal-ai-agent, Restate tour of agents, Inngest AgentKit |

Signals and queries aren't a scenario of their own: they come up in the approval, subscription and agent samples.

Line counts below are non-blank, non-comment lines of the workflow code only (no activities, no hosting). The saga
counts include the message records, which the workflow tools mostly don't need as separate types. Take them as a
rough measure of size, nothing more.

## The scenarios

### 1. Money transfer, from Temporal's .NET `Saga` sample

**Original.** [samples-dotnet/src/Saga](https://github.com/temporalio/samples-dotnet/tree/main/src/Saga), MIT. One
`RunAsync` method: push a compensation lambda, run the withdraw activity, push another, run the deposit, run a step
that throws; in the `catch`, run the lambdas in reverse. 49 lines.

**Saga.** [MoneyTransfer/Transfer.cs](SagaWorkflows/MoneyTransfer/Transfer.cs), 59 lines (11 of them message records).
Each step's reply moves the saga on; `Withdrawn` / `Deposited` flags are the compensation stack. Three
`Compensate(TStep, ExceptionInfo)` methods call one private `undo()` that returns the undo messages newest first.

**As good or better.**
- Compensation is a decision made from saved state, not a list of closures that only exists inside a running method.
  `undo()` is a pure function with a unit test and no host.
- `[TerminalFailure]` on `InsufficientFundsException` skips retries; the ledger outage retries twice and then
  compensates. Temporal needs `NonRetryableErrorTypes` on the activity options for the same split.
- The saga ends up `Status = Compensated` with a `CompensationLog` entry, and the failed step is dead lettered with a
  `compensated` tag. Temporal shows a failed workflow and its history.
- The withdraw and the saga update commit together. A Temporal activity that writes to your database isn't in the same
  transaction as the workflow's progress.

**Clumsier.**
- One `Compensate` per step type. `Compensate(ITransferStep, ...)` would compile and work for the saga's *own*
  handlers, but a `Compensate` for a message the saga *sends* only asks for `Fault<T>` of the exact declared type
  (`SagaCompensations.FindCalls` closes `Fault<>` over it, and `Fault<T>` isn't covariant). Not a big deal, but
  surprising; worth a sentence in the docs.
- The undo messages go out together, not one after the other. Fine here; see trip booking for when it isn't.

**Missing.** Nothing.

### 2. Trip booking, from Restate's sagas example

**Original.** [restatedev/examples, typescript/patterns-use-cases/src/sagas](https://github.com/restatedev/examples/tree/main/typescript/patterns-use-cases/src/sagas),
MIT. Book flight, car, hotel with `ctx.run`; register each cancel *before* its booking; on a `TerminalError`, run the
cancels in reverse, awaiting each. Anything else is retried forever. About 30 lines of handler.

**Saga.** [TripBooking/Trip.cs](SagaWorkflows/TripBooking/Trip.cs), 90 lines (19 of them records: six step messages,
six replies, six undo messages and replies).

**As good or better.**
- `Compensate(BookHotel, RemoteFailure<BookingRefusedException>)` plus `[TerminalFailure]` on the exception is exactly
  Restate's `TerminalError` rule, and it works when the provider is in another service, matched by the exception's
  type hierarchy.
- An outage isn't compensated. It's retried, then parked in the dead letter queue with the trip waiting. Restate
  retries forever; parking it where an operator can see and replay it is at least as good.
- `[RetainWhenCompleted]` keeps the finished trip, so "what happened to trip X" has an answer.

**Clumsier.**
- Undo that waits for each cancellation is a small state machine: an `Attempted` stack, three
  `Handle(XCancelled)` methods and an `undoNext()`. In Restate it is `for (const c of compensations.reverse()) await c()`.
  This is the one place in the eight samples where the saga is plainly more code for the same idea.
- Restate cancels the step that failed too, in case it went through on the other side. The saga does the same, but
  only because I wrote it that way (`Attempted` gets the step before it is sent). The default thing to write with a
  saga is to undo confirmed steps only.

**Missing.** Nothing, but a documented "undo stack" pattern would save people the thinking.

### 3. Order approval, from Dapr's order processing workflow

**Original.** [dapr/dotnet-sdk examples/Workflow/WorkflowConsoleApp](https://github.com/dapr/dotnet-sdk/tree/master/examples/Workflow/WorkflowConsoleApp),
Apache 2.0. Notify, reserve stock, ask for approval over a threshold and `WaitForExternalEventAsync` with a timeout,
take payment, update stock, `SetCustomStatus` along the way. 90 lines. The same shape is Durable Functions' human
interaction pattern and Temporal's expense sample.

**Saga.** [OrderApproval/Order.cs](SagaWorkflows/OrderApproval/Order.cs), 75 lines, plus three HTTP endpoints: start,
answer, status.

**As good or better.** This is the case sagas were made for. The wait is the absence of a message; the timeout is a
`TimeoutMessage`; the "custom status" is a property; the status query is a document load by id
(`GET /orders/{id}` with `[Entity]`). An answer that arrives after the timeout goes to `NotFound` and does nothing.
Shorter than the original, and it reads well.

**Clumsier.** `NotFound(ManagerDecision)` has to be written, or a late answer is an `UnknownSagaException`. A
`TimeoutMessage` gets that for free; signals don't.

**Missing.** Nothing.

### 4. Subscription billing, from Temporal's subscription tutorial

**Original.** [subscription-workflow-project-template-typescript](https://github.com/temporalio/subscription-workflow-project-template-typescript)
(no license file; domain only). Welcome email; `condition(cancelled, trialPeriod)`; then
`while (true) { charge; sleep(billingPeriod) }` up to a maximum; signals to cancel and change the price; queries for
the billing state. 79 lines.

**Saga.** [Subscriptions/Subscription.cs](SagaWorkflows/Subscriptions/Subscription.cs), 74 lines, plus endpoints for
the signals and the query.

**As good or better.**
- The loop is one `BillingPeriodEnded(Id, Period, Delay) : TimeoutMessage(Delay)` that each period schedules for the
  next. Months of sleeping are rows in the durable scheduled store. There is no history to grow, so no
  continue-as-new.
- Queries are a document read, and with `[RetainWhenCompleted(Days = 30)]` they still answer after the subscription
  ends. No worker has to replay anything to answer one.
- Cancel takes effect at once. In the original it's only noticed when the `sleep` wakes up.

**Clumsier.** The stale timeout guard is yours to write: `ended.Period == BillingPeriodNumber`. The workflow can't get
an old timer back, because it isn't waiting on one any more.

**Missing.** Nothing.

### 5. Fan-out / fan-in, from Durable Functions' cloud backup (and Restate's parallelizework)

**Original.** [Durable Task fan-out/fan-in](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-fan-in-fan-out)
and [Restate's fan_out_worker](https://github.com/restatedev/examples/tree/main/typescript/patterns-use-cases/src/parallelizework).
List the files, start a copy per file, `Task.WhenAll`, add up the bytes. About 20 to 25 lines.

**Saga.** [FanOutFanIn/Backup.cs](SagaWorkflows/FanOutFanIn/Backup.cs), 44 lines, plus a partitioned topology in
`Program.cs`.

**As good or better.**
- A copy that fails for good comes back as `Fault<BackupFile>` and the backup carries on with the rest. In Durable
  Functions, `Task.WhenAll` throws and the orchestration fails unless you wrap each task yourself.
- Each copy is an ordinary message, so the copies spread across every node like any other work.

**Clumsier, and the main finding.**
- **Concurrent replies.** The saga docs' first answer for concurrent replies is `IRevisioned` plus a
  `RetryWithCooldown` on `ConcurrencyException`. With 25 files that's not enough: in two runs, 8 and then 9 of the
  25 `FileBackedUp` replies were dead lettered after their four retries, and the backup never finished. What works is
  partitioned local queues grouped by saga id (`MessagePartitioning.UseInferredMessageGrouping()` plus
  `PublishToPartitionedLocalMessaging(...)` naming each reply type), with zero conflicts. The docs do recommend that
  for scatter-gather, but you have to know it, you configure it far from the saga, and you have to list every reply
  type by hand. A workflow tool never makes you think about this: one workflow instance runs one thing at a time.
- **Bookkeeping.** The pending set (which also makes a duplicate reply harmless), and the empty directory: with no
  files nothing ever replies, so `Start` has to notice that itself. `Task.WhenAll` over nothing just returns.
- **Size.** Every reply rewrites the saga document, so a 10,000-file fan-out writes O(N²) bytes. Keep counters
  rather than lists, or split into child sagas per chunk. Temporal has the same class of problem with its 50k event
  history limit.

**Missing.** A way to say "process this saga's messages one at a time" on the saga itself. See the summary.

### 6. Child workflows, from Durable Functions' sub-orchestrations

**Original.** [Durable Task sub-orchestrations](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-sub-orchestrations):
`ProvisionNewDevices` calls `CallSubOrchestratorAsync("DeviceProvisioningOrchestration", deviceId)` per device and
awaits them all; the child creates a package, sends the URL to the device and waits for the device's
acknowledgement. About 25 lines for both.

**Saga.** [ChildWorkflows/](SagaWorkflows/ChildWorkflows/): `DeviceRollout` (parent) and `DeviceProvisioning` (child),
79 lines. Starting a child is sending its start message; the child tells the parent with a message that names the
parent (`DeviceProvisioned(DeviceRolloutId, ...)`); a failed child is a `DeviceProvisioningFailed` result, not an
exception.

**As good or better.** Each child is a first-class saga: queryable by itself, startable without a parent, retained,
compensated on its own. No shared history to outgrow.

**Clumsier.** The biggest gap in line count of the eight, about three to one. All of it is plumbing a workflow tool
does for you: carrying the parent id, a result message type per outcome, the parent's pending set, and the same fan-in
partitioning as scenario 5. There's also no cancellation that flows from parent to children, and no "parent close
policy"; you'd write those as messages.

**Missing.** Nothing that a feature must fix; it's the fan-in switch from scenario 5 plus a documented pattern.

### 7. Polling, from Temporal's polling samples and Durable Functions' monitor

**Original.** [samples-dotnet/src/Polling](https://github.com/temporalio/samples-dotnet/tree/main/src/Polling), MIT
(three variants; "infrequent" is an activity whose retry policy is the polling interval), and the
[Durable Task monitor pattern](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-monitor)
(a loop with `CreateTimer` until done or expired). 25 to 30 lines.

**Saga.** [Polling/ExportWatch.cs](SagaWorkflows/Polling/ExportWatch.cs), 39 lines.

**As good or better.** The loop is a `PollExport(Id, Delay) : TimeoutMessage(Delay)` that reschedules itself; backoff
is one line. The saga's handler calls the external API directly, so there is no activity indirection. Temporal's
"infrequent" variant maps straight onto a Wolverine error policy (`ScheduleRetry`) on a plain handler, if you prefer
that shape.

**Clumsier.** Only in testing. `PlayScheduledMessagesAsync()` plays *everything* scheduled in a tracked session, so an
overall deadline scheduled at the start would fire together with the first poll. I designed around it (give up after N
polls instead of after a deadline). Temporal's test environment skips time forward to the next timer, which is what
I wanted here.

**Missing.** A test helper that advances time. See the summary.

### 8. Agent loop, from temporal-ai-agent and Restate's tour of agents

**Original.** [temporal-ai-agent](https://github.com/temporal-community/temporal-ai-agent), MIT, and the human approval
agents in [restatedev/ai-examples](https://github.com/restatedev/ai-examples). Call the model, run the tools it asks
for, feed the results back, until it answers; a person confirms risky tools. Temporal's workflow is 331 lines of
Python, and it's built as signal handlers plus a `wait_condition` loop over workflow state, with continue-as-new when
the history gets long. In other words, a state machine written inside a workflow.

**Saga.** [AgentLoop/AgentRun.cs](SagaWorkflows/AgentLoop/AgentRun.cs), 91 lines, against a scripted fake model. It
does less than temporal-ai-agent (no goals, no MCP), so the line counts don't compare. It does have parallel tool
calls, approval with a timeout, a turn limit, and a failed tool that comes back through `Compensate(RunTool, ...)` as
an observation for the model.

**As good or better.** Each turn is a message, so the loop is natural. The conversation is a property, readable by any
UI at any time. No history limit, so no continue-as-new.

**Clumsier.** The pending tool calls bookkeeping, and tool results are another fan-in that needs the partitioned
queues.

**Missing.** Nothing specific to sagas. Streaming partial output to a UI is outside both models.

## Across the scenarios

- **Reading the flow.** This is the honest win for workflows. A linear flow reads top to bottom in one method; in a
  saga, the order of steps is spread over `Start`, `Handle` and `Compensate` methods, and you follow it by the message
  types. For linear orchestration (money transfer, trip booking, a child workflow) the workflow code is shorter and
  easier to follow. For flows that mostly wait and react (approval, subscription, polling, an agent loop) the saga is
  as short or shorter, and clearer, because "waiting" isn't code at all.
- **No determinism rules.** Nothing in a saga has to replay, so there's nothing to keep deterministic: no
  `Workflow.UtcNow`, no `Workflow.NewGuid`, no "don't do I/O here".
- **Versioning running instances.** A saga is data plus methods. Changing the code for in-flight sagas is a data
  question (a new property defaults, an old one is ignored), not a replay question; there's nothing like Temporal's
  `Patched()`. The real hazard is renaming a message type that's in flight or sitting in the scheduled store. That
  needs a message type alias, and a doc page.
- **Querying a running instance.** Better with sagas: it's a document, readable from an endpoint, CritterWatch or SQL,
  running or retained.
- **Visibility.** This is the real gap. A document saga shows its current state, `LastModified`, its status and the
  compensation log; it doesn't show *how it got there*. Every workflow tool's UI shows the step-by-step history of an
  instance, and people expect that.
- **Testing.** Pure-function saga methods are easier to unit test than any workflow, and tracked sessions cover the
  rest. Temporal's time-skipping test environment beats `PlayScheduledMessagesAsync()` for anything with more than one
  timer.
- **Durability and restarts.** Saga state is a Marten document, written in the same transaction as the outbox; the
  durable inbox and the scheduled store cover the rest. I didn't add a restart test here; the #5654 and #5660 spikes
  already showed restarts for their designs, and for plain sagas it's the long-standing behavior.

## Real gaps worth features

| Gap | Smallest Wolverine feature that closes it |
|---|---|
| Fan-in: concurrent replies to one saga fight over the document, and the fix is app-wide routing that lists every reply type | A per-saga switch, for example `[SequentialSaga]` on the class or `opts.Policies.ProcessSagaMessagesSequentially()`, that routes every message the saga handles (replies and `Fault<T>` included) through partitioned local queues grouped by saga id. It's `UseInferredMessageGrouping()` plus a topology, configured from the saga type instead of by hand |
| Visibility: no step-by-step history of a document saga | #5415's `ISagaHistoryDiagnostics` / event-sourced sagas plus the CritterWatch timeline, which are already planned. For document sagas, the smallest version is a per-saga message log keyed by the saga-id header Wolverine already stamps |
| Testing timers: `PlayScheduledMessagesAsync()` plays everything at once | `session.PlayScheduledMessagesAsync(advanceBy: 1.Hours())`: play only what's due inside the window, in due order, and keep the rest. Bobcat vocabulary for the same |

Not gaps, on this evidence: loops (a self-scheduling `TimeoutMessage`), child workflows (two sagas and a result
message), queries (a document), signals (a message), versioning (it's data).

## Only documentation

- **"Sagas for Temporal / Durable Functions users"**: these eight samples side by side with the originals, as the
  #5405 decision already asked for.
- **Patterns page**: undo stack with confirmed cancellations (trip booking), a loop as a self-scheduling timeout with
  a stale guard (subscription), fan-in with partitioning and a pending set (backup), parent and child sagas (devices),
  polling with backoff, and an agent loop.
- **Compensation details**: a `Compensate` for a *sent* step must name the concrete message type; `NotFound` for
  signals that can arrive late; `[RetainWhenCompleted]` for "what happened to X".
- **Versioning in-flight sagas**: what's safe to change, and message type aliases for renames.
- **Fan-in sizing**: counters, not lists, for big fan-outs; chunk into child sagas.

## Recommendation on #5405

Replace it with docs and three targeted saga features. Don't build the workflow engine for 7.0.

The samples don't show anything a workflow method can do that a saga can't. What they show is that workflow code is
easier to *read* for linear orchestration and child workflows, and that sagas make you think about fan-in
concurrency. The first is real but modest (two to three times the lines in the worst case, scenario 6). The second is
fixable with a small feature.

The source generator design (#5660) is also a poor fit for what the samples need. It forbids loops and `try`/`catch`
around a wait, and those are exactly the shapes where a workflow method reads best: the subscription loop, polling,
the agent loop, and Restate's `for (c of compensations.reverse()) await c()`. What's left that it allows (straight
lines and `if`/`else`) is where sagas are only moderately longer. Journal replay (#5654) allows loops, but it brings the
determinism rules, `finally` blocks that don't run while it waits, and O(N) reads per resume, which are precisely the
things sagas don't have.

So: the sequential-saga switch, the time-advancing test helper, and #5415's history and timeline (already planned),
plus the docs above with these samples as their source. If people still ask for workflow syntax once those are out, a
*small* builder for linear flows (send, await reply, compensate) that generates a saga would be the next thing to try.
It shouldn't be a general `async` method splitter.

## A Wolverine issue found along the way

**The application assembly is the test assembly under Alba.** With this 7.0 build, `WebApplication` +
`builder.Host.UseWolverine(...)` + `app.RunJasperFxCommands(args)`, started from xUnit v3 (Microsoft.Testing.Platform)
through `AlbaHost.For<Program>()` (Alba 9.0.0), Wolverine resolves `ApplicationAssembly` to **`SagaWorkflows.Tests`**,
not `SagaWorkflows`. No handlers or sagas are found, and every send fails with `IndeterminateRoutesException`.
`dotnet run -- describe` on the same app resolves `SagaWorkflows` correctly.

- Repro: remove `opts.ApplicationAssembly = typeof(Program).Assembly;` from `SagaWorkflows/Program.cs` and run any
  test class.
- Where: `WolverineOptions.establishApplicationAssembly` / `determineCallingAssembly()` in
  `src/Wolverine/WolverineOptions.Assemblies.cs`. The walk falls through to `Assembly.GetEntryAssembly()`, which under a
  `WebApplicationFactory` is the test project. `IHostEnvironment.ApplicationName`, which `WebApplicationFactory` sets
  to the app's assembly, would be the better answer there.
- I didn't check whether 6.x behaves the same. The older samples in this repository run under Alba without setting
  `ApplicationAssembly`, so this looks like a 7.0 regression, but that's unconfirmed.

## What changed with Wolverine 7.0.0-alpha.3

The evaluation above was written against `7.0.0-local.1`. The samples now run on `7.0.0-alpha.3`, which has the saga
features this evaluation and its follow-up issues asked for. Scenario by scenario, what got shorter or went away:

| Scenario | Then | Now |
|---|---|---|
| 1. Money transfer | `Withdrawn` / `Deposited` flags, three `Compensate` methods, a private `undo()` | `PushUndo` as each step happens, one `Compensate(ITransferStep, ExceptionInfo)` returning `UndoMessages()` (#5672, #5673). An event-sourced twin keeps the whole history (#5415) |
| 2. Trip booking | An `Attempted` list, three `Handle(XCancelled)` methods and an `undoNext()`: "the one place where the saga is plainly more code" | `PushUndo(..., confirmedBy:)` before each booking and `UndoSequentially(completeWhenDone: true)`. The hand-written state machine is gone (#5673) |
| 3. Order approval | An empty `NotFound(ManagerDecision)` so a late answer isn't an `UnknownSagaException` | `[DiscardIfSagaMissing]` on the `Handle` method (#5674); `[RecordSagaHistory]` shows how the order got where it is (#5665) |
| 4. Subscription | The stale-timeout guard `ended.Period == BillingPeriodNumber`, and a `NotFound` per signal | `[ReplacesEarlierTimeouts]` on the billing timeout and `[SagaMissing(Discard)]` on the saga (#5674) |
| 5. Fan-out / fan-in | `PublishToPartitionedLocalMessaging` listing every reply type by hand | Global partitioning with inferred grouping over a marker interface, `IFanInReply` (the documented answer from #5663 / #5666) |
| 7. Polling | Gave up after N polls, because a deadline couldn't be tested | A real one-hour deadline, tested with `PlayScheduledMessagesAsync(advanceBy:)` and Bobcat's `WhenTimePasses` (#5664) |
| 8. Agent loop | Nothing to see while it waits on a person | Idle runs found by `LastModified` (#5558) |

On the three gaps in "Real gaps worth features":

- **Fan-in.** No per-saga switch (#5663 is parked for 7.1). Global partitioning plus `UseInferredMessageGrouping()` is
  the answer, and with a marker interface the configuration names no reply types. The scenarios that need it (backup,
  child workflows, agent loop) run without a concurrency conflict in the specs.
- **Visibility.** Closed for both kinds of saga: an event-sourced saga's stream is its history, with time travel to any
  version or time, and a document saga can record a row per handled message. Both are read through
  `ISagaHistoryDiagnostics`, which is what CritterWatch's planned saga timeline reads.
- **Testing timers.** Closed: `PlayScheduledMessagesAsync(advanceBy:)` and `WhenTimePasses`.

What the alpha.3 rebuild turned up:

- **Letting time pass doesn't compensate.** `PlayScheduledMessagesAsync(advanceBy:)` runs a local scheduled message
  inline, so a failure of the saga's own handler is thrown at the test instead of being retried, dead lettered and
  compensated. The polling spec delivers the due poll to its queue instead (`WhenReceived` with `DeliveryOptions`).
- **An event-sourced `Compensate` returning `(event, UndoSequence)`** unwinds, but `completeWhenDone` is lost and the
  compensation log records only the last step. Returning the `UndoSequence` alone works, so `EventSourcedTrip` does.
- **`saga-flow` quirks.** Under `[SagaMissing(Discard)]` the start message is drawn as "logged and dropped" too; a
  `Compensate(ITransferStep, ...)` or `Compensate(object, ...)` adds a message box for the interface (or `Object`), and
  the catch-all is drawn as covering `TripBooked`, which the trip only sends when it's done.
- **Durable faults across a crash** (`GivenTheServiceCrashesAfterItsNextDeadLetter` / `WhenTheServiceRestarts`) work
  in Wolverine's own two-service `SagaFaults` sample. Here, with the fault going to a durable *local* queue in the
  same service, the restart never delivered it within 30 seconds. Not investigated further; the samples don't claim it.

Counts: 47 Bobcat specs in `SagaWorkflows.Specs` and the original 32 tests, all green, three runs in a row.
