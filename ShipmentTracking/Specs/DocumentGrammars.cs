using Bobcat;
using Bobcat.Engine;
using Bobcat.Engine.Verification;
using Polecat;

namespace ShipmentTracking.Specs;

/// <summary>
/// The vocabulary this application needs and Bobcat does not ship.
/// </summary>
/// <remarks>
/// <para>
/// ShipmentTracking is not event sourced. It is a Polecat <b>document</b> store with declarative
/// persistence, cascading messages and a saga — which is a very ordinary shape for a Wolverine
/// application, and one the shipped Gherkin grammar cannot describe. Of the eight store steps in
/// <c>CritterStackFixture</c>, exactly two apply here: <c>When {command} is received</c> and
/// <c>Then {message} is sent</c>. Everything else is addressed to an event store
/// (<c>Given no events for …</c>, <c>Then {event} is emitted</c>) or to a stream-keyed projection
/// (<c>Then the {readmodel} read model contains</c>).
/// </para>
/// <para>
/// So this module supplies the missing third: arrange a document, assert a document. It is written
/// against <c>IDocumentSession</c> rather than anything ShipmentTracking-specific, and the type is
/// a capture — which is the argument for lifting something like it into Bobcat rather than making
/// every document-backed application write its own.
/// </para>
/// <para>
/// <b>The steps read worse than they should, and that is a Bobcat constraint rather than a choice.</b>
/// The capture vocabulary is closed — <c>type</c>, <c>aggregate</c>, <c>command</c>, <c>event</c>,
/// <c>readmodel</c>, <c>message</c> — so there is no <c>{document}</c> to name one with, and the
/// generic <c>{type}</c> forces "documents of type Shipment" where "Shipments" is what a reader
/// wants to read.
/// </para>
/// </remarks>
public class DocumentGrammars : Fixture
{
    private IStepContext Ctx => Context ?? throw new InvalidOperationException(
        "No IStepContext is set — a document grammar step ran outside a scenario.");

    /// <summary>
    /// Arrange: the documents this scenario starts from. One row per document, columns are its
    /// properties — the document twin of <c>Given events for {aggregate}</c>.
    /// </summary>
    [Given("documents of type {type}")]
    public async Task GivenDocuments(Type document, StepTable rows)
    {
        var session = Ctx.GetService<IDocumentSession>();

        foreach (var row in rows.AsDictionaries())
        {
            session.Store(bindRow(document, row));
        }

        Ctx.RecordTouchedType(document);
        await session.SaveChangesAsync(Ctx.Cancellation);
    }

    /// <summary>
    /// Assert: load one document by id and compare the columns the row names — deliberately a
    /// subset check, matching what <c>Then {event} is emitted</c> became in 0.14.0, so a scenario
    /// says only what it cares about.
    /// </summary>
    [Then("the {type} with id {string} has")]
    public async Task ThenDocumentHas(Type document, string id, StepTable expected)
    {
        if (expected.Rows.Count != 1)
            throw new SpecCriticalException(
                $"'Then the {document.Name} with id …' takes exactly one row of expected values, but got {expected.Rows.Count}.");

        var loaded = await loadAsync(document, id);

        if (loaded is null)
            throw new SpecAssertionException(
                $"Expected a {document.Name} document with id '{id}', but none exists.");

        var failures = new List<string>();

        foreach (var (column, want) in expected.AsDictionaries()[0])
        {
            var property = document.GetProperty(column);
            if (property is null)
            {
                failures.Add($"  '{column}' is no such property on {document.Name}");
                continue;
            }

            var actual = property.GetValue(loaded)?.ToString() ?? "";
            if (!string.Equals(actual, want, StringComparison.Ordinal))
                failures.Add($"  {column}: expected '{want}' but was '{actual}'");
        }

        // Every mismatch at once. One assertion per run makes a wrong document take as many runs
        // to describe as it has wrong fields.
        if (failures.Count > 0)
            throw new SpecAssertionException(
                $"The {document.Name} with id '{id}' did not match:\n{string.Join("\n", failures)}");
    }

    /// <summary>Assert a document was never written, or was removed.</summary>
    [Then("no {type} exists with id {string}")]
    public async Task ThenNoDocument(Type document, string id)
    {
        var loaded = await loadAsync(document, id);

        if (loaded is not null)
            throw new SpecAssertionException(
                $"Expected no {document.Name} with id '{id}', but one exists.");
    }

    /// <summary>
    /// Bind a table row onto a document. Bobcat has exactly this, and better —
    /// <c>RecordBuilding.Build</c>, which the shipped grammars use — but it is
    /// <c>internal</c>, so a grammar module outside the Bobcat assemblies cannot call it and has
    /// to carry a poorer copy. Worth lifting to public: every custom grammar that takes a table
    /// needs it, and each one will reimplement it slightly differently.
    /// </summary>
    private static object bindRow(Type type, IReadOnlyDictionary<string, string> row)
    {
        var instance = Activator.CreateInstance(type)
                       ?? throw new SpecCriticalException($"{type.Name} has no parameterless constructor.");

        foreach (var (column, value) in row)
        {
            var property = type.GetProperty(column)
                           ?? throw new SpecCriticalException(
                               $"'{column}' matches no property on {type.Name}.");

            property.SetValue(instance, Convert.ChangeType(
                property.PropertyType == typeof(Guid) ? Guid.Parse(value) : value,
                property.PropertyType));
        }

        return instance;
    }

    /// <summary>
    /// Load by runtime <see cref="Type"/>. Polecat's <c>LoadAsync</c> is generic only, and a
    /// <c>{type}</c>-captured step has nothing but a Type — so the call is reflected. The same
    /// friction would meet any document grammar Bobcat shipped.
    /// </summary>
    private async Task<object?> loadAsync(Type document, string id)
    {
        var session = Ctx.GetService<IQuerySession>();

        var method = typeof(IQuerySession)
            .GetMethods()
            .Single(m => m.Name == nameof(IQuerySession.LoadAsync)
                         && m.IsGenericMethodDefinition
                         && m.GetParameters() is [{ ParameterType.Name: nameof(Guid) }, _])
            .MakeGenericMethod(document);

        var task = (Task)method.Invoke(session, [Guid.Parse(id), Ctx.Cancellation])!;
        await task;

        return task.GetType().GetProperty("Result")!.GetValue(task);
    }
}
