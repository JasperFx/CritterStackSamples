namespace CritterWatch.Samples.Testing;

/// <summary>
/// Thin client-side projection of CritterWatch's <c>ServiceSummary</c> document as returned by
/// <c>GET /api/critterwatch/services</c>. The console serializes that endpoint with
/// <c>JsonSerializerDefaults.Web</c> (camelCase), so <c>ServiceSummary.Id</c> arrives as <c>"id"</c>.
/// </summary>
/// <remarks>
/// Only the fields the sample test battery cares about are modelled — chiefly <see cref="Id"/>, which
/// holds the monitored service's name (the document's identity; the console's
/// <c>/services/{serviceName}</c> route loads by this same key). Additional fields on the real
/// <c>ServiceSummary</c> (versions, message stores, brokers, event stores, …) are ignored on parse.
/// </remarks>
/// <param name="Id">The monitored service's name / document identity.</param>
/// <param name="Version">The service's reported application version, when present.</param>
/// <param name="Label">An optional operator-facing label for the service.</param>
public sealed record ServiceSummaryDto(string Id, string? Version = null, string? Label = null);
