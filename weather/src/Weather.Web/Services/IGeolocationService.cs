namespace Weather.Web.Services;

/// <summary>Why a geolocation attempt did not yield a position.</summary>
public enum GeolocationError
{
    /// <summary>A position was obtained — no error.</summary>
    None,

    /// <summary>The user declined to share their location.</summary>
    PermissionDenied,

    /// <summary>The browser could not determine a position (no signal, hardware, etc.).</summary>
    PositionUnavailable,

    /// <summary>The request took too long.</summary>
    Timeout,

    /// <summary>The browser has no Geolocation API at all.</summary>
    NotSupported,

    /// <summary>Something else went wrong (e.g. the JS bridge threw).</summary>
    Unknown,
}

/// <summary>
/// The outcome of asking the browser for the user's location. Always returned —
/// never thrown — so the UI can branch on <see cref="Success"/> and present a
/// graceful fallback when the position is unavailable.
/// </summary>
public sealed record GeolocationResult(
    bool Success,
    double Latitude,
    double Longitude,
    double? Accuracy,
    GeolocationError Error)
{
    /// <summary>A non-successful result carrying only the reason.</summary>
    public static GeolocationResult Failed(GeolocationError error) =>
        new(false, 0, 0, null, error);
}

/// <summary>
/// Reads the user's location from the browser. An interface so the dashboard
/// can be rendered in tests without a real browser or JS runtime.
/// </summary>
public interface IGeolocationService
{
    /// <summary>
    /// Ask the browser for the current position. Resolves to a
    /// <see cref="GeolocationResult"/> in all cases (granted, denied,
    /// unavailable, unsupported); it does not throw on user refusal.
    /// </summary>
    ValueTask<GeolocationResult> GetCurrentPositionAsync(CancellationToken cancellationToken = default);
}
