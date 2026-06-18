using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Weather.Core.Abstractions;
using Weather.Core.Models;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Serialization;

namespace Weather.Infrastructure.Nws;

/// <summary>
/// Typed <see cref="HttpClient"/> over the NWS API. Owns HTTP concerns,
/// conditional GETs and telemetry; it performs NO caching (that is the job of
/// the caches and <c>WeatherService</c>). The <see cref="HttpClient"/> itself —
/// base address, User-Agent, Accept header and the resilience pipeline — is
/// configured in <c>DependencyInjection</c>.
/// </summary>
internal sealed class NwsApiClient(
    HttpClient httpClient,
    WeatherTelemetry telemetry,
    ILogger<NwsApiClient> logger) : INwsApiClient
{
    private const string PointsEndpoint = "points";
    private const string ForecastEndpoint = "forecast";

    public async Task<PointMetadata?> GetPointMetadataAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        var requestUri = $"points/{coordinate.ToApiString()}";
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            RecordDuration(PointsEndpoint, (int)response.StatusCode, timestamp, conditional: false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("NWS has no grid coverage for {Coordinate}.", coordinate);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                RecordFailure(PointsEndpoint, response.StatusCode);
                logger.LogWarning("NWS /points returned {StatusCode} for {Coordinate}.",
                    (int)response.StatusCode, coordinate);
                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<NwsPointResponse>(WeatherJson.Options, cancellationToken)
                .ConfigureAwait(false);

            var properties = payload?.Properties;
            if (properties?.GridId is null)
            {
                logger.LogWarning("NWS /points payload for {Coordinate} was missing grid metadata.", coordinate);
                return null;
            }

            var location = properties.RelativeLocation?.Properties;
            return new PointMetadata(
                Query: coordinate,
                Grid: new GridPoint(properties.GridId, properties.GridX, properties.GridY),
                ForecastUrl: properties.Forecast ?? string.Empty,
                ForecastHourlyUrl: properties.ForecastHourly ?? string.Empty,
                City: location?.City,
                State: location?.State,
                TimeZone: properties.TimeZone,
                RadarStation: properties.RadarStation);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            RecordFailure(PointsEndpoint, statusCode: null);
            logger.LogWarning(ex, "NWS /points request failed for {Coordinate}.", coordinate);
            return null;
        }
    }

    public async Task<ForecastFetchResult> GetForecastAsync(
        GridPoint grid, string? etag = null, CancellationToken cancellationToken = default)
    {
        var requestUri = string.Create(
            CultureInfo.InvariantCulture,
            $"gridpoints/{grid.GridId}/{grid.GridX},{grid.GridY}/forecast");

        var conditional = !string.IsNullOrEmpty(etag);
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (conditional)
            {
                // Use TryAddWithoutValidation so weak ("W/...") and quoted tags
                // round-trip exactly as NWS sent them.
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            }

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            RecordDuration(ForecastEndpoint, (int)response.StatusCode, timestamp, conditional);

            switch (response.StatusCode)
            {
                case HttpStatusCode.NotModified:
                    return ForecastFetchResult.NotModified(
                        response.Headers.ETag?.ToString() ?? etag,
                        response.Headers.CacheControl?.MaxAge);

                case HttpStatusCode.NotFound:
                    logger.LogInformation("NWS has no forecast for grid {Grid}.", grid);
                    return ForecastFetchResult.NotFound;

                case HttpStatusCode.TooManyRequests:
                    telemetry.RecordThrottled(ForecastEndpoint);
                    logger.LogWarning("NWS throttled the forecast request for grid {Grid} (HTTP 429).", grid);
                    return ForecastFetchResult.Unavailable;
            }

            if (!response.IsSuccessStatusCode)
            {
                RecordFailure(ForecastEndpoint, response.StatusCode);
                logger.LogWarning("NWS /forecast returned {StatusCode} for grid {Grid}.",
                    (int)response.StatusCode, grid);
                return ForecastFetchResult.Unavailable;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<NwsForecastResponse>(WeatherJson.Options, cancellationToken)
                .ConfigureAwait(false);

            if (payload?.Properties is not { } properties)
            {
                logger.LogWarning("NWS /forecast payload for grid {Grid} was empty.", grid);
                return ForecastFetchResult.Unavailable;
            }

            var forecast = MapForecast(grid, properties);
            return ForecastFetchResult.Success(
                forecast,
                response.Headers.ETag?.ToString(),
                response.Headers.CacheControl?.MaxAge);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            RecordFailure(ForecastEndpoint, statusCode: null);
            logger.LogWarning(ex, "NWS /forecast request failed for grid {Grid}.", grid);
            return ForecastFetchResult.Unavailable;
        }
    }

    private static Forecast MapForecast(GridPoint grid, NwsForecastProperties properties)
    {
        var source = properties.Periods ?? [];
        var periods = new List<ForecastPeriod>(source.Count);
        foreach (var p in source)
        {
            periods.Add(new ForecastPeriod(
                Number: p.Number,
                Name: p.Name ?? string.Empty,
                StartTime: p.StartTime,
                EndTime: p.EndTime,
                IsDaytime: p.IsDaytime,
                Temperature: p.Temperature,
                TemperatureUnit: p.TemperatureUnit ?? "F",
                ProbabilityOfPrecipitation: p.ProbabilityOfPrecipitation?.Value is { } pop
                    ? (int)Math.Round(pop, MidpointRounding.AwayFromZero)
                    : null,
                WindSpeed: p.WindSpeed ?? string.Empty,
                WindDirection: p.WindDirection ?? string.Empty,
                ShortForecast: p.ShortForecast ?? string.Empty,
                DetailedForecast: p.DetailedForecast ?? string.Empty,
                Icon: p.Icon ?? string.Empty));
        }

        return new Forecast(grid, properties.GeneratedAt, properties.UpdateTime, periods);
    }

    private void RecordDuration(string endpoint, int statusCode, long startTimestamp, bool conditional) =>
        telemetry.RecordNwsRequest(
            endpoint,
            statusCode,
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
            conditional);

    private void RecordFailure(string endpoint, HttpStatusCode? statusCode) =>
        telemetry.RecordError(endpoint, statusCode is null ? 0 : (int)statusCode);
}
