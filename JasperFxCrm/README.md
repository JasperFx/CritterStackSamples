# JasperFxCrm — an Event Modeled CRM, built spec-first

The customer-relationship system JasperFx runs its own business on, written in the open as the
sample for the **Event Modeling with the Critter Stack** tutorial series on
[jasperfx.net](https://jasperfx.net/tutorials/event-modeling/). Leads from the website, support
agreements and their renewals, contracts generated from Word templates and sent for signature,
consulting time, the conversations behind all of it.

Everything here is public. The real deployment is a thin private host that composes this project
(`AddJasperFxCrm` / `MapJasperFxCrm`) and swaps the fakes under `Vendors/` for the real Stripe,
DocuSign, QuickBooks, Slack and Discord clients. **This project references no vendor SDK**, so it runs
from a clone with nothing but PostgreSQL.

## Running

```bash
cd ..                       # CritterStackSamples root
docker compose up -d        # PostgreSQL on 5433
cd JasperFxCrm
dotnet test                 # the Bobcat specifications
dotnet run --project JasperFxCrm -- describe-event-model   # the model, declared and derived, with hotspots
```

## How it is built

1. A chapter's Event Model is **declared** in `JasperFxCrm/EventModel/{Chapter}Model.cs`.
2. Its **specifications** are written in `JasperFxCrm.Specs/{Chapter}/`, one `[BobcatFeature]` class
   per slice, and run red.
3. `dotnet run --project JasperFxCrm -- scaffold` writes the handlers and endpoints the model declares
   but the code lacks; the slices are implemented until the specs are green.
4. `describe-event-model` compares what was declared with what the handlers actually do.

Chapters, in the order they land: Leads, Contracts, Signing, Support agreements, the ported customer
core (accounts, checkout, entitlements, trials), Engagement, Community, Invoicing.

| Chapter | State |
|---|---|
| Leads | declared; `CaptureLead` specified, red |
