using Weather.Core.Abstractions;
using Weather.Core.Models;

namespace Weather.Api.Tests.Fakes;

/// <summary>
/// A deterministic <see cref="INwsApiClient"/> for end-to-end API tests. It lets
/// the real weather service, SQLite caches and endpoints run unchanged while
/// removing the network: any normal coordinate resolves to grid AKQ/83,61 with a
/// sunny forecast, hourly periods and a current observation; the ocean sentinel
/// (0, 0) reports no coverage.
/// </summary>
internal sealed class FakeNwsApiClient : INwsApiClient
{
    private const string NearestStationId = "KPHF";
    private static readonly GeoCoordinate CellCenter = new(37.0900, -76.4500);

    public Task<PointMetadata?> GetPointMetadataAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        // (0, 0) — open ocean — stands in for "outside NWS coverage".
        if (Math.Abs(coordinate.Latitude) < 0.001 && Math.Abs(coordinate.Longitude) < 0.001)
        {
            return Task.FromResult<PointMetadata?>(null);
        }

        var metadata = new PointMetadata(
            Query: coordinate,
            Grid: new GridPoint("AKQ", 83, 61),
            ForecastUrl: "https://api.weather.gov/gridpoints/AKQ/83,61/forecast",
            ForecastHourlyUrl: "https://api.weather.gov/gridpoints/AKQ/83,61/forecast/hourly",
            City: "Bethel Manor",
            State: "VA",
            TimeZone: "America/New_York",
            RadarStation: "KAKQ");

        return Task.FromResult<PointMetadata?>(metadata);
    }

    public Task<ForecastFetchResult> GetForecastAsync(
        GridPoint grid, string? etag = null, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var forecast = new Forecast(grid, now, now, new[]
        {
            new ForecastPeriod(1, "This Afternoon", now, now.AddHours(4), true, 90, "F", 3,
                "5 to 12 mph", "S", "Sunny", "Sunny, with a high near 90.", "icon-day"),
            new ForecastPeriod(2, "Tonight", now.AddHours(4), now.AddHours(16), false, 74, "F", null,
                "12 mph", "S", "Mostly Clear", "Mostly clear, with a low around 74.", "icon-night"),
        });

        return Task.FromResult(ForecastFetchResult.Success(forecast, "\"e\"", TimeSpan.FromMinutes(30), CellCenter));
    }

    public Task<IReadOnlyList<ForecastPeriod>> GetHourlyForecastAsync(
        GridPoint grid, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<ForecastPeriod> hourly = new[]
        {
            new ForecastPeriod(1, string.Empty, now, now.AddHours(1), true, 90, "F", 2,
                "6 mph", "S", "Sunny", string.Empty, "icon-hour-1"),
            new ForecastPeriod(2, string.Empty, now.AddHours(1), now.AddHours(2), true, 89, "F", 2,
                "7 mph", "S", "Sunny", string.Empty, "icon-hour-2"),
        };

        return Task.FromResult(hourly);
    }

    public Task<string?> GetNearestStationIdAsync(
        GridPoint grid, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(NearestStationId);

    public Task<Observation?> GetLatestObservationAsync(
        GridPoint grid, string stationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Observation?>(BuildObservation(stationId));

    public Task<Observation?> GetLatestObservationAsync(
        GridPoint grid, CancellationToken cancellationToken = default) =>
        Task.FromResult<Observation?>(BuildObservation(NearestStationId));

    public Task<IReadOnlyList<WeatherAlert>> GetActiveAlertsAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WeatherAlert>>([]);

    private static Observation BuildObservation(string stationId) =>
        new(
            StationId: stationId,
            StationName: "Newport News",
            Timestamp: DateTimeOffset.UtcNow,
            TextDescription: "Sunny",
            Icon: "icon-obs",
            TemperatureF: 88,
            DewpointF: 70,
            RelativeHumidity: 55,
            WindSpeedMph: 8,
            WindGustMph: 14,
            WindDirection: "S",
            PressureInHg: 30.05,
            VisibilityMiles: 10.0);
}
