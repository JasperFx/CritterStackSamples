# Contributor API — Critter Stack Conversion

## Original Project

**Repository:** [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture)
**License:** MIT
**Stack:** ASP.NET Core, FastEndpoints, Mediator (source-generated), EF Core, Ardalis.Result, Ardalis.GuardClauses, Ardalis.Specification, Vogen (value objects), Ardalis.SmartEnum

Steve Smith's Clean Architecture template (~16k stars). Uses FastEndpoints for the web layer, source-generated mediator for CQRS, and Vogen for strongly-typed value objects. 4 projects with a Contributor aggregate demonstrating DDD tactical patterns.

## What Changed

### Removed (4 projects collapsed to 1)
- **Core project** — aggregate, value objects (Vogen), domain events, specifications, smart enums
- **UseCases project** — commands, queries, handlers, DTOs
- **Infrastructure project** — EF Core DbContext, entity configurations, repositories, event dispatching, email services
- **FastEndpoints** — endpoint classes, validators, mappers, request/response models
- **Mediator** — source-generated mediator + pipeline behaviors
- **Ardalis packages** — Result, GuardClauses, Specification, SharedKernel, SmartEnum
- **Vogen** — value object code generation (ContributorId, ContributorName)

### Before vs After

| Aspect | Original | Converted |
|--------|----------|-----------|
| Projects | 4 (Core, UseCases, Infrastructure, Web) | 1 |
| C# files | ~70 | 6 |
| Endpoint framework | FastEndpoints + Mediator | Wolverine.HTTP |
| Value objects | Vogen (code-generated) | Plain properties |
| Result pattern | Ardalis.Result wrapping | ProblemDetails + concrete types |
| Repository | Ardalis.Specification | Direct Marten session |
| Domain events | Interceptor + mediator notifications | Wolverine cascading messages |
| Database | EF Core (SQLite/SQL Server) | Marten (PostgreSQL) |

## Running

Requires PostgreSQL.

```bash
dotnet run
```

Tests:
```bash
cd Tests && dotnet test
```

Swagger UI at `/swagger`.
