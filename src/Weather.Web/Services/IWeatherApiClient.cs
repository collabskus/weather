namespace Weather.Web.Services;

/// <summary>
/// The dashboard's view of the backend Weather API. An interface so the Blazor
/// components can be unit-tested with a fake (see Weather.Web.Tests) instead of
/// a live HTTP endpoint.
/// </summary>
public interface IWeatherApiClient
{
    /// <summary>
    /// Forecast for the grid cell containing the coordinate. Returns
    /// <c>null</c> when the API reports the location is outside NWS coverage
    /// (HTTP 404).
    /// </summary>
    Task<ForecastDto?> GetForecastAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default);

    /// <summary>
    /// Primary forecast plus any already-warm neighbouring cells. Returns
    /// <c>null</c> when the location is outside NWS coverage.
    /// </summary>
    Task<NeighborhoodDto?> GetNeighborhoodAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default);

    /// <summary>
    /// The full area view: the user's cell plus every neighbouring cell ordered
    /// by distance, each with its full data, and any active alerts. Returns
    /// <c>null</c> when the location is outside NWS coverage.
    /// </summary>
    Task<AreaDto?> GetAreaAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default);
}
