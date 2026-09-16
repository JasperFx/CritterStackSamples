using System.Text;
using Bobcat.EventModel;

/// <summary>
/// Derives the Stoat plan from the curated model. One node per slice, and the node's gate is the
/// slice's spec identities — `{Feature}/{Scenario}`, exactly the string a Bobcat scenario's Uid
/// carries. That is why this is derived and not written by hand: every scenario name is an
/// identity, so renaming one in the model silently orphans a gate, and the previous hand-written
/// plan had eleven identities matching no scenario at all.
/// </summary>
internal static class StoatPlan
{
    internal static string From(CuratedModelFile model)
    {
        // Who emits what, so a dependency is a fact about the model rather than a guess.
        var emitters = new Dictionary<string, string>();
        foreach (var slice in model.Slices)
        {
            foreach (var @event in slice.Events) emitters.TryAdd(@event, NodeId(slice.Name));
        }

        var chapter = model.Slices.Select(x => x.Chapter).FirstOrDefault(x => x is not null) ?? model.Model;

        var writer = new StringBuilder();
        writer.AppendLine("# DERIVED from CritterCrush.emodel.yaml by models/Scaffolder — do not hand-edit.");
        writer.AppendLine("# Regenerate with the --plan flag whenever the model changes.");
        writer.AppendLine("schema: 1");
        writer.AppendLine($"plan: {model.Model.ToLowerInvariant()}-{chapter.ToLowerInvariant()}");
        writer.AppendLine($"title: {model.Model} — the {chapter} chapter, built from the model");
        writer.AppendLine("nodes:");

        foreach (var slice in model.Slices)
        {
            writer.AppendLine($"  - id: {NodeId(slice.Name)}");
            writer.AppendLine("    kind: slice");
            writer.AppendLine($"    slice: {slice.Name}");
            if (slice.Pattern is not null) writer.AppendLine($"    pattern: {slice.Pattern.ToLowerInvariant()}");
            if (slice.Domain is not null) writer.AppendLine($"    domain: {slice.Domain}");

            // Two edges, and only two, because anything more mechanical builds a cycle: Confirm
            // arranges a cancellation in its refusal scenario while Cancel arranges a confirmation
            // in its own. So a command depends on whoever emits the event that STARTS its stream,
            // and a view on whoever emits what it consumes.
            var dependsOn = (slice.Pattern == "View"
                    ? slice.ConsumedEvents.Select(e => emitters.GetValueOrDefault(e))
                    : slice.Specifications?.Scenarios
                        .Select(s => s.Given.FirstOrDefault()?.Event)
                        .Where(e => e is not null)
                        .Select(e => emitters.GetValueOrDefault(e!)) ?? [])
                .Where(x => x is not null && x != NodeId(slice.Name))
                .Distinct()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            if (dependsOn.Count > 0)
            {
                writer.AppendLine("    depends_on:");   // stoat reads plan YAML with the underscored convention
                foreach (var id in dependsOn) writer.AppendLine($"      - {id}");
            }

            var feature = slice.Specifications?.Feature ?? slice.Name;
            var scenarios = slice.Specifications?.Scenarios ?? [];
            if (scenarios.Count > 0)
            {
                writer.AppendLine("    specs:");
                foreach (var scenario in scenarios) writer.AppendLine($"      - {feature}/{scenario.Name}");
            }
        }

        return writer.ToString();
    }

    // ProposeHomeCheckAppointment -> propose-home-check-appointment
    private static string NodeId(string sliceName)
    {
        var writer = new StringBuilder();
        foreach (var c in sliceName)
        {
            if (char.IsUpper(c) && writer.Length > 0) writer.Append('-');
            writer.Append(char.ToLowerInvariant(c));
        }

        return writer.ToString();
    }
}
