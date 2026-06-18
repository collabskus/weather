using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Nws;
using Weather.Infrastructure.Tests.Fakes;

namespace Weather.Infrastructure.Tests.Nws;

public sealed class NwsApiClientTests
{
    private static readonly GeoCoordinate SampleCoordinate = new(37.0879, -76.4505);
    private static readonly GridPoint SampleGrid = new("AKQ", 83, 61);

    private static NwsApiClient CreateClient(StubHttpMessageHandler handler, WeatherTelemetry telemetry)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.weather.gov/"),
        };
        return new NwsApiClient(httpClient, telemetry, NullLogger<NwsApiClient>.Instance);
    }

    [Test]
    public async Task GetPointMetadataParsesGridAndLocation()
    {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, NwsPayloads.Points));
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        var metadata = await client.GetPointMetadataAsync(SampleCoordinate);

        metadata.ShouldNotBeNull();
        metadata!.Grid.ShouldBe(SampleGrid);
        metadata.City.ShouldBe("Bethel Manor");
        metadata.State.ShouldBe("VA");
        metadata.TimeZone.ShouldBe("America/New_York");
        metadata.RadarStation.ShouldBe("KAKQ");
        metadata.ForecastUrl.ShouldBe("https://api.weather.gov/gridpoints/AKQ/83,61/forecast");
    }

    [Test]
    public async Task GetPointMetadataRequestsTheRoundedCoordinatePath()
    {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, NwsPayloads.Points));
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        await client.GetPointMetadataAsync(SampleCoordinate);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.RequestUri!.AbsolutePath.ShouldBe("/points/37.0879,-76.4505");
    }

    [Test]
    public async Task GetPointMetadataReturnsNullWhenUncovered()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        var metadata = await client.GetPointMetadataAsync(SampleCoordinate);

        metadata.ShouldBeNull();
    }

    [Test]
    public async Task GetPointMetadataReturnsNullOnServerError()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        (await client.GetPointMetadataAsync(SampleCoordinate)).ShouldBeNull();
    }

    [Test]
    public async Task GetForecastSuccessMapsPeriodsEtagAndMaxage()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, NwsPayloads.Forecast);
            response.Headers.ETag = new EntityTagHeaderValue("\"abc123\"");
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(45) };
            return response;
        });
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        var result = await client.GetForecastAsync(SampleGrid);

        result.Outcome.ShouldBe(NwsFetchOutcome.Success);
        result.ETag.ShouldBe("\"abc123\"");
        result.MaxAge.ShouldBe(TimeSpan.FromMinutes(45));

        result.Forecast.ShouldNotBeNull();
        var forecast = result.Forecast!;
        forecast.Grid.ShouldBe(SampleGrid);
        forecast.Periods.Count.ShouldBe(2);

        var first = forecast.Periods[0];
        first.Name.ShouldBe("This Afternoon");
        first.Temperature.ShouldBe(90);
        first.TemperatureUnit.ShouldBe("F");
        first.IsDaytime.ShouldBeTrue();
        first.ProbabilityOfPrecipitation.ShouldBe(3);
        first.ShortForecast.ShouldBe("Sunny");

        // A null POP value must map to null, not zero.
        forecast.Periods[1].ProbabilityOfPrecipitation.ShouldBeNull();
    }

    [Test]
    public async Task GetForecastRequestsTheGridForecastPath()
    {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, NwsPayloads.Forecast));
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        await client.GetForecastAsync(SampleGrid);

        handler.LastRequest!.RequestUri!.AbsolutePath.ShouldBe("/gridpoints/AKQ/83,61/forecast");
    }

    [Test]
    public async Task GetForecastSendsConditionalHeaderAndMaps304()
    {
        const string etag = "\"abc123\"";
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new EntityTagHeaderValue(etag);
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(20) };
            return response;
        });
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        var result = await client.GetForecastAsync(SampleGrid, etag);

        result.Outcome.ShouldBe(NwsFetchOutcome.NotModified);
        result.ETag.ShouldBe(etag);
        result.MaxAge.ShouldBe(TimeSpan.FromMinutes(20));

        handler.LastRequest!.Headers.TryGetValues("If-None-Match", out var values).ShouldBeTrue();
        values!.ShouldContain(etag);
    }

    [Test]
    public async Task GetForecastWithoutEtagSendsNoConditionalHeader()
    {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, NwsPayloads.Forecast));
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        await client.GetForecastAsync(SampleGrid);

        handler.LastRequest!.Headers.Contains("If-None-Match").ShouldBeFalse();
    }

    [Test]
    public async Task GetForecastMaps404ToNotFound()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        (await client.GetForecastAsync(SampleGrid)).Outcome.ShouldBe(NwsFetchOutcome.NotFound);
    }

    [Test]
    public async Task GetForecastMaps429ToUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        (await client.GetForecastAsync(SampleGrid)).Outcome.ShouldBe(NwsFetchOutcome.Unavailable);
    }

    [Test]
    public async Task GetForecastMaps500ToUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        (await client.GetForecastAsync(SampleGrid)).Outcome.ShouldBe(NwsFetchOutcome.Unavailable);
    }

    [Test]
    public async Task GetForecastMapsTransportFailureToUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))
        {
            ThrowOnSend = new HttpRequestException("connection reset"),
        };
        using var telemetry = new WeatherTelemetry();
        var client = CreateClient(handler, telemetry);

        (await client.GetForecastAsync(SampleGrid)).Outcome.ShouldBe(NwsFetchOutcome.Unavailable);
    }
}
