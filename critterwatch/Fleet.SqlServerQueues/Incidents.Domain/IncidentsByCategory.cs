using Polecat.Projections;

namespace Incidents.Domain;

/// <summary>
/// Per-<see cref="IncidentCategory"/> rollup. The headline async projection for the Incidents group — the
/// operator can pause / rebuild it in CritterWatch, and a rebuild against the steady-state event flow is
/// non-trivial enough to exercise the rebuild UI realistically.
///
/// <para>
/// The <c>Id</c> is the category name as a <b>string</b> (not the enum). Accepted Id types are
/// Guid / int / long / string; an enum-typed Id leaves the store's auto-detected IdType null.
/// </para>
/// </summary>
public class IncidentsByCategoryView
{
    /// <summary>The <see cref="IncidentCategory"/> name (e.g. "Software"). String-typed so it's accepted as the Id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Every incident ever categorised into this bucket.</summary>
    public int Total { get; set; }

    /// <summary>Still in <see cref="IncidentStatus.Resolved"/> or earlier.</summary>
    public int Active { get; set; }

    /// <summary>Reached <see cref="IncidentStatus.Resolved"/>.</summary>
    public int Resolved { get; set; }

    /// <summary>Reached <see cref="IncidentStatus.Closed"/>.</summary>
    public int Closed { get; set; }

    public DateTimeOffset? LastEventAt { get; set; }
}

/// <summary>
/// Multi-stream Polecat projection grouping incidents by category. The IncidentResolved + IncidentClosed
/// events embed the category at emit time so the projection doesn't need to round-trip the aggregate.
///
/// <para>
/// Declared <c>partial</c> so Polecat's bundled JasperFx.Events source generator (hooked in via the direct
/// Polecat package ref in this csproj) emits the apply dispatcher. Versus the Marten flagship, only the
/// projection base-class namespace changes (<c>Polecat.Projections</c>).
/// </para>
/// </summary>
public partial class IncidentsByCategoryProjection
    : MultiStreamProjection<IncidentsByCategoryView, string>
{
    public IncidentsByCategoryProjection()
    {
        Name = "IncidentsByCategory";

        Identity<IncidentCategorised>(e => e.Category.ToString());
        Identity<IncidentResolved>(e => (e.Category ?? IncidentCategory.Software).ToString());
        Identity<IncidentClosed>(e => (e.Category ?? IncidentCategory.Software).ToString());
    }

    public void Apply(IncidentCategorised _, IncidentsByCategoryView view)
    {
        view.Total++;
        view.Active++;
    }

    public void Apply(IncidentResolved _, IncidentsByCategoryView view)
    {
        view.Resolved++;
        if (view.Active > 0) view.Active--;
    }

    public void Apply(IncidentClosed _, IncidentsByCategoryView view) => view.Closed++;
}
