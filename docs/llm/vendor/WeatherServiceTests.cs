using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather.Core.Abstractions;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Nws;
using Weather.Infrastructure.Services;
using Weather.Infrastructure.Tests.Fakes;

namespace Weather.Infrastructure.Tests.Services;

public sealed class WeatherServiceTests
{
    private static readonly GeoCoordinate Coord = new(37.0879, -76.4505);
    private static readonly GridPoint Grid = new("AKQ", 83, 61);
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);

    private static PointMetadata Meta() =>
        new(Coord, Grid, "forecast-url", "hourly-url", "Bethel Manor", "VA", "America/New_York", "KAKQ");

    private static Forecast ForecastFor(GridPoint grid) =>
        new(grid, Now, Now, new[]
        {
            new ForecastPeriod(1, "This Afternoon", Now, Now.AddHours(4), true, 90, "F", 3,
                "5 mph", "S", "Sunny", "Detailed.", "icon"),
        });

    private static CachedForecast FreshForecast(GridPoint grid) =>
        new(ForecastFor(grid), "\"e\"", Now, Now.AddHours(1));

    private static CachedForecast StaleForecast(GridPoint grid) =>
        new(ForecastFor(grid), "\"e\"", Now.AddHours(-2), Now.AddHours(-1));

    private static CachedPointMetadata FreshMeta() => new(Meta(), Now, Now.AddDays(30));

    private static CachedPointMetadata StaleMeta() => new(Meta(), Now.AddDays(-60), Now.AddDays(-30));

    private sealed class Harness
    {
        public IPointMetadataCache MetadataCache { get; } = Substitute.For<IPointMetadataCache>();
        public IForecastCache ForecastCache { get; } = Substitute.For<IForecastCache>();
        public ICellExtrasCache ExtrasCache { get; } = Substitute.For<ICellExtrasCache>();
        public INwsApiClient Nws { get; } = Substitute.For<INwsApiClient>();
        public INeighborhoodWarmer Warmer { get; } = Substitute.For<INeighborhoodWarmer>();
        public WeatherService Service { get; }

        public Harness()
        {
            Service = new WeatherService(
                MetadataCache, ForecastCache, ExtrasCache, Nws, Warmer,
                new WeatherTelemetry(), new MutableTimeProvider(Now),
                Options.Create(new NwsClientOptions()), NullLogger<WeatherService>.Instance);
        }
    }

    [Test]
    public async Task GetForecastOnFullMissFetchesStoresAndSchedulesWarming()
    {
        var h = new Harness();
        h.Nws.GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(Meta());
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.Success(ForecastFor(Grid), "\"e\"", TimeSpan.FromMinutes(30)));

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        result!.Grid.ShouldBe(Grid);
        await h.MetadataCache.Received(1).UpsertAsync(Arg.Any<CachedPointMetadata>(), Arg.Any<CancellationToken>());
        await h.ForecastCache.Received(1).UpsertAsync(Arg.Any<CachedForecast>(), Arg.Any<CancellationToken>());
        h.Warmer.Received(1).RequestWarming(Grid);
    }

    [Test]
    public async Task GetForecastWithFreshMetadataDoesNotCallPoints()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns(FreshForecast(Grid));

        await h.Service.GetForecastAsync(Coord);

        await h.Nws.DidNotReceive().GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastWithFreshForecastDoesNotCallForecast()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns(FreshForecast(Grid));

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        await h.Nws.DidNotReceive().GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastStaleThenNotModifiedRenewsTtlAndReturnsCached()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns(StaleForecast(Grid));
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.NotModified("\"e2\"", TimeSpan.FromMinutes(20)));

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        result!.Periods[0].ShortForecast.ShouldBe("Sunny");
        await h.ForecastCache.Received(1).UpsertAsync(
            Arg.Is<CachedForecast>(c =>
                c.ETag == "\"e2\"" &&
                c.RetrievedAtUtc == Now &&
                c.ExpiresAtUtc == Now.AddMinutes(20)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastStaleThenUnavailableServesStaleWithoutUpsert()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns(StaleForecast(Grid));
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.Unavailable);

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        result!.Grid.ShouldBe(Grid);
        await h.ForecastCache.DidNotReceive().UpsertAsync(Arg.Any<CachedForecast>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastNotFoundWithoutCacheReturnsNull()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.NotFound);

        (await h.Service.GetForecastAsync(Coord)).ShouldBeNull();
    }

    [Test]
    public async Task GetForecastWithUnresolvableMetadataAndNoCacheReturnsNull()
    {
        var h = new Harness();
        // metadata cache empty + NWS resolve returns null
        h.Nws.GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns((PointMetadata?)null);

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldBeNull();
        await h.Nws.DidNotReceive().GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastWithUnresolvableMetadataButStaleCacheServesStaleMetadata()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(StaleMeta());
        h.Nws.GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns((PointMetadata?)null);
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.Success(ForecastFor(Grid), "\"e\"", TimeSpan.FromMinutes(30)));

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        await h.Nws.Received(1).GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetNeighborhoodReturnsOnlyAlreadyFreshNeighbors()
    {
        var h = new Harness();
        var neighborA = new GridPoint("AKQ", 82, 61);
        var neighborB = new GridPoint("AKQ", 84, 61);

        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns(FreshForecast(Grid));
        h.ForecastCache.GetAsync(neighborA, Arg.Any<CancellationToken>()).Returns(FreshForecast(neighborA));
        h.ForecastCache.GetAsync(neighborB, Arg.Any<CancellationToken>()).Returns(FreshForecast(neighborB));
        // every other surrounding cell returns null (the NSubstitute default)

        var result = await h.Service.GetNeighborhoodForecastAsync(Coord);

        result.ShouldNotBeNull();
        result!.Primary.Grid.ShouldBe(Grid);
        result.Neighbors.Count.ShouldBe(2);
        result.Neighbors.Select(n => n.Grid).ShouldBe(new[] { neighborA, neighborB }, ignoreOrder: true);
        h.Warmer.Received(1).RequestWarming(Grid);
    }

    [Test]
    public async Task GetNeighborhoodWithoutMetadataReturnsNull()
    {
        var h = new Harness();
        h.Nws.GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns((PointMetadata?)null);

        (await h.Service.GetNeighborhoodForecastAsync(Coord)).ShouldBeNull();
    }

    [Test]
    public async Task GetForecastByGridDoesNotScheduleWarming()
    {
        var h = new Harness();
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.Success(ForecastFor(Grid), "\"e\"", TimeSpan.FromMinutes(30)));

        var result = await h.Service.GetForecastByGridAsync(Grid);

        result.ShouldNotBeNull();
        h.Warmer.DidNotReceive().RequestWarming(Arg.Any<GridPoint>());
    }
}
