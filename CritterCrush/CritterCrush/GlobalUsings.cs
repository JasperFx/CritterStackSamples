// The scaffolder emits slice files with no usings — the shapes it generates are the same in
// every slice, so they belong here once rather than repeated eleven times.
global using Microsoft.AspNetCore.Mvc;
global using Wolverine.Http;
global using Wolverine.Marten;
global using Wolverine.Persistence.EventSourcing;
global using Marten;
global using Marten.Events.Projections;
global using Marten.Events.Aggregation;
global using Wolverine.Persistence;
