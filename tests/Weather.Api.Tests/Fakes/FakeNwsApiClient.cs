using Weather.Core.Abstractions;
using Weather.Core.Models;

namespace Weather.Api.Tests.Fakes;

/// <summary>
/// A deterministic <see cref="INwsApiClient"/> for end-to-end API tests. It lets
/// the real weather service, SQLite cache and endpoints run unchanged while
/// removing the network: any normal coordinate resolves to grid AKQ/83,61 with a
/// sunny forecast, and the ocean sentinel (0, 0) reports no coverage.
/// </summary>
internal sealed class FakeNwsApiClient : INwsApiClient
{
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

        return Task.FromResult(ForecastFetchResult.Success(forecast, "\"e\"", TimeSpan.FromMinutes(30)));
    }
}
