// The scaffolder emits slice files with no usings — the shapes it generates are the same in
// every slice, so they belong here once rather than repeated nineteen times.
global using Microsoft.AspNetCore.Mvc;
global using Wolverine.Http;
global using Wolverine.Marten;
global using Wolverine.Persistence.EventSourcing;
global using Marten;
global using Marten.Events.Projections;
global using Marten.Events.Aggregation;
global using Wolverine.Persistence;
global using JasperFx;

// The two chapters, each in its own namespace, and each visible to the other. This is not
// incidental: ProposeHomeCheckAppointment (Scheduling) handles HomeCheckAssignmentAccepted, whose
// record Volunteering owns because Volunteering is the slice that emits it. The cross-chapter link
// on the Event Model is a cross-namespace reference in the code.
global using CritterCrush.Scheduling;
global using CritterCrush.Volunteering;
