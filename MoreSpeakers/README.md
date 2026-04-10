# MoreSpeakers (Document DB) — Critter Stack Conversion

## Original Project

**Repository:** [cwoodruff/morespeakers-com](https://github.com/cwoodruff/morespeakers-com)
**License:** MIT
**Stack:** ASP.NET Core 10, Razor Pages, EF Core (SQL Server), ASP.NET Identity, AutoMapper, Azure Functions, .NET Aspire

A mentorship platform connecting aspiring speakers with experienced mentors. 7 projects across 5 layers (Domain, Data, Managers, Web, Functions) with Razor Pages UI, HTMX interactivity, and Azure-first infrastructure.

> **See also:** `MoreSpeakersES/` (coming) — the same domain rebuilt with Marten event sourcing and aggregate handlers.

## What Changed

### Removed (5 layers collapsed to 1)
- **Domain project** — separate entity models
- **Data project** — EF Core DbContext, DataStores (repositories), AutoMapper profiles, entity configurations
- **Managers project** — thin business logic wrappers with logging/telemetry
- **Web project** — Razor Pages UI (API endpoints preserved)
- **Functions project** — Azure Functions for email
- **AutoMapper** — Domain ↔ Data model mapping
- **EF Core** — SQL Server, migrations, join tables (UserExpertise, MentorshipExpertise)
- **ASP.NET Identity** — IdentityUser, roles, policies

### Simplification Highlights

| Aspect | Original | Converted |
|--------|----------|-----------|
| Projects | 7 | 1 (+1 test) |
| Layers per operation | 5 (Endpoint → Manager → DataStore → AutoMapper → EF Core) | 1 (Endpoint → Marten session) |
| Join tables | 3 (UserExpertise, MentorshipExpertise, UserSocialMediaSite) | 0 (nested collections) |
| Entity mapping | AutoMapper profiles | None needed |
| Database | SQL Server + Azure SQL | PostgreSQL (Marten) |

### Document Model

The original used 16 EF Core entities with join tables for many-to-many relationships. Marten stores these as documents with nested collections:

- **Speaker** — embeds `Expertise` (string list), `SocialLinks` (nested objects). Replaces: User + UserExpertise + UserSocialMediaSite + SpeakerType.
- **Mentorship** — embeds `FocusAreas` (string list), denormalized mentor/mentee names. Replaces: Mentorship + MentorshipExpertise join table.
- **ExpertiseCategory** — embeds `Skills` (string list). Replaces: Sector + ExpertiseCategory + Expertise 3-level hierarchy.

## Running

Requires PostgreSQL.

```bash
dotnet run
```

Tests (requires PostgreSQL):

```bash
cd ../MoreSpeakers.Tests
dotnet test
```

Swagger UI at `/swagger`.
