# Sagas vs Durable Workflows

Eight sample workflows published for Temporal, Restate, Durable Functions and Dapr Workflow, rebuilt as
ordinary Wolverine sagas on Marten. The point is to see, scenario by scenario, where a saga is as good as a
"workflow as code" tool, where it's clumsier, and where something is really missing.
[EVALUATION.md](EVALUATION.md) has the findings.

Every domain was reimplemented from the original's description. No code was copied.

| Folder | Original | Tool | What it shows |
|--------|----------|------|---------------|
| [MoneyTransfer](SagaWorkflows/MoneyTransfer/) | [samples-dotnet/src/Saga](https://github.com/temporalio/samples-dotnet/tree/main/src/Saga) (MIT) | Temporal | Withdraw, deposit, a failing last step, undo newest first. `CompensatingSaga`, `Compensate(T, ExceptionInfo)`, `[TerminalFailure]` |
| [TripBooking](SagaWorkflows/TripBooking/) | [patterns-use-cases/src/sagas](https://github.com/restatedev/examples/tree/main/typescript/patterns-use-cases/src/sagas) (MIT) | Restate | Flight, car, hotel. A refusal cancels in reverse order, each cancellation confirmed before the next. `RemoteFailure<T>`, `[RetainWhenCompleted]` |
| [OrderApproval](SagaWorkflows/OrderApproval/) | [examples/Workflow/WorkflowConsoleApp](https://github.com/dapr/dotnet-sdk/tree/master/examples/Workflow/WorkflowConsoleApp) (Apache 2.0) | Dapr Workflow | Manager approval with a timeout, signals and a status query over HTTP |
| [Subscriptions](SagaWorkflows/Subscriptions/) | [subscription-workflow-project-template-typescript](https://github.com/temporalio/subscription-workflow-project-template-typescript) (no license file; domain only) | Temporal | Trial, then monthly billing for months; cancel and change the price while it runs; query it, also after it ends |
| [FanOutFanIn](SagaWorkflows/FanOutFanIn/) | [Fan-out/fan-in (cloud backup)](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-fan-in-fan-out), also Restate's [parallelizework](https://github.com/restatedev/examples/tree/main/typescript/patterns-use-cases/src/parallelizework) | Durable Functions, Restate | Copy every file in parallel, add up the bytes. `Fault<T>` for a branch that fails, partitioned queues for the fan-in |
| [ChildWorkflows](SagaWorkflows/ChildWorkflows/) | [Sub-orchestrations](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-sub-orchestrations) | Durable Functions | A rollout saga starts a provisioning saga per device and waits for them all |
| [Polling](SagaWorkflows/Polling/) | [samples-dotnet/src/Polling](https://github.com/temporalio/samples-dotnet/tree/main/src/Polling) (MIT), Durable Functions' [monitor pattern](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-monitor) | Temporal, Durable Functions | Ask a slow system until the job is done, backing off, then give up |
| [AgentLoop](SagaWorkflows/AgentLoop/) | [temporal-ai-agent](https://github.com/temporal-community/temporal-ai-agent) (MIT), Restate's [ai-examples](https://github.com/restatedev/ai-examples) tour of agents | Temporal, Restate | An LLM tool loop: parallel tool calls, a person approves the risky tool, a turn limit, a failed tool fed back to the model |

The "activities" (the bank, the airline, the LLM...) are ordinary Wolverine handlers over in-memory fakes, in the
same folder as each saga. In a real system they would often be in another service; nothing in the sagas would
change.

## Running it

You need the .NET 10 SDK and PostgreSQL.

**PostgreSQL.** The connection string is `ConnectionStrings:Marten` in `SagaWorkflows/appsettings.json`. It points at
`localhost:5442`, database `saga_eval`, user `postgres`/`postgres`, because that is where this was built. The
repository's own `docker compose up -d` Postgres is on port 5433; to use it, create the database and override the
connection string:

```bash
export ConnectionStrings__Marten="Host=localhost;Port=5433;Database=saga_eval;Username=postgres;Password=postgres"
```

Marten creates its schema (`saga_workflows`) on the first run.

**Wolverine packages.** Saga compensation (`CompensatingSaga`, `Compensate`, `RemoteFailure<T>`, `[TerminalFailure]`,
durable faults) is in [wolverine PR #5659](https://github.com/JasperFx/wolverine/pull/5659) and not on nuget.org yet.
`nuget.config` reads `WolverineFx*` packages from `../../local-packs/wolverine-eval` (that is, `~/code/local-packs/wolverine-eval`
next to this repository). These were packed from wolverine commit
`5e696ddc544daffaf8e99564e70d15fb511d12ed` (branch `gh-5554-5556-specs`) as version `7.0.0-local.1`:

```bash
git -C ~/code/wolverine worktree add --detach ~/code/wolverine-eval-packs 5e696ddc5
cd ~/code/wolverine-eval-packs
for p in src/Wolverine/Wolverine.csproj src/Wolverine.Analyzers/Wolverine.Analyzers.csproj \
         src/Wolverine.RuntimeCompilation/Wolverine.RuntimeCompilation.csproj \
         src/Persistence/Wolverine.RDBMS/Wolverine.RDBMS.csproj src/Persistence/Wolverine.Postgresql/Wolverine.Postgresql.csproj \
         src/Persistence/Wolverine.Marten/Wolverine.Marten.csproj src/Http/Wolverine.Http/Wolverine.Http.csproj \
         src/Http/Wolverine.Http.Marten/Wolverine.Http.Marten.csproj; do
  dotnet pack $p -c Release -p:Version=7.0.0-local.1 -o ~/code/local-packs/wolverine-eval
done
```

Once #5659 ships, change `WolverineVersion` in `SagaWorkflows.csproj` and delete `nuget.config`.

**Tests.**

```bash
dotnet test --project SagaWorkflows.Tests
dotnet test --project SagaWorkflows.Tests --filter-class "*TripBookingTests"
```

32 tests, a few seconds. Most send a message (or an HTTP request through Alba) and wait for everything it set
off with a tracked session. Time is moved forward with `PlayScheduledMessagesAsync()`. A few call the saga's methods
directly, because they are pure functions.

## Things to look at

- `Program.cs` is the whole infrastructure: Marten, durable local queues, two error policies, and the partitioned
  queues that the fan-in sagas need.
- Every saga starts with a comment naming the original it was rebuilt from.
- Each saga file holds its messages and the saga. The tool's code puts the steps in one method; here they are
  `Start`, `Handle` and `Compensate` methods on one class.
