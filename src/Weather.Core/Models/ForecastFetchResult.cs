namespace Weather.Core.Models;

/// <summary>The four ways an attempt to fetch a forecast from NWS can resolve.</summary>
public enum NwsFetchOutcome
{
    /// <summary>A fresh forecast payload was returned (HTTP 200).</summary>
    Success,

    /// <summary>The cached copy is still current (HTTP 304 Not Modified).</summary>
    NotModified,

    /// <summary>NWS does not cover this location/grid (HTTP 404).</summary>
    NotFound,

    /// <summary>NWS could not be reached or kept failing (throttled, 5xx, timeout).</summary>
    Unavailable,
}

/// <summary>
/// Result of an <see cref="Abstractions.INwsApiClient.GetForecastAsync"/> call.
/// Models conditional-GET semantics explicitly so the caller can keep serving a
/// stale-but-valid cached copy when appropriate.
/// </summary>
public sealed record ForecastFetchResult
{
    public required NwsFetchOutcome Outcome { get; init; }
    public Forecast? Forecast { get; init; }
    public string? ETag { get; init; }
    public TimeSpan? MaxAge { get; init; }

    /// <summary>
    /// Approximate geographic centre of the grid cell, derived from the polygon
    /// geometry NWS returns with the forecast. Used to order neighbouring cells
    /// by true distance from the user. Null when geometry was absent.
    /// </summary>
    public GeoCoordinate? Center { get; init; }

    public static ForecastFetchResult Success(
        Forecast forecast, string? etag, TimeSpan? maxAge, GeoCoordinate? center = null) =>
        new() { Outcome = NwsFetchOutcome.Success, Forecast = forecast, ETag = etag, MaxAge = maxAge, Center = center };

    public static ForecastFetchResult NotModified(string? etag, TimeSpan? maxAge) =>
        new() { Outcome = NwsFetchOutcome.NotModified, ETag = etag, MaxAge = maxAge };

    public static readonly ForecastFetchResult NotFound = new() { Outcome = NwsFetchOutcome.NotFound };
    public static readonly ForecastFetchResult Unavailable = new() { Outcome = NwsFetchOutcome.Unavailable };
}
