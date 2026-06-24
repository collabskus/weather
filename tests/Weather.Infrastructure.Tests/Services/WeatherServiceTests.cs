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
        public IAlertCache AlertCache { get; } = Substitute.For<IAlertCache>();
        public IForecastNegativeCache NegativeCache { get; } = Substitute.For<IForecastNegativeCache>();
        public INwsApiClient Nws { get; } = Substitute.For<INwsApiClient>();
        public INeighborhoodWarmer Warmer { get; } = Substitute.For<INeighborhoodWarmer>();
        public WeatherService Service { get; }

        // REAL coalescers: their logic is pure and in-process, so the hot path
        // runs through them exactly as in production while these tests keep
        // asserting cache-aside behaviour. There is a separate flight set for
        // forecasts, extras, metadata and alerts — matching the registrations.
        public IRequestCoalescer<GridPoint> ForecastCoalescer { get; } = new RequestCoalescer<GridPoint>();
        public IRequestCoalescer<GridPoint> ExtrasCoalescer { get; } = new RequestCoalescer<GridPoint>();
        public IRequestCoalescer<GeoCoordinate> MetadataCoalescer { get; } = new RequestCoalescer<GeoCoordinate>();
        public IRequestCoalescer<string> AlertCoalescer { get; } = new RequestCoalescer<string>();

        public Harness(NwsClientOptions? options = null)
        {
            Service = new WeatherService(
                MetadataCache, ForecastCache, ExtrasCache, AlertCache, NegativeCache, Nws, Warmer,
                ForecastCoalescer, ExtrasCoalescer, MetadataCoalescer, AlertCoalescer,
                new WeatherTelemetry(), new MutableTimeProvider(Now),
                Options.Create(options ?? new NwsClientOptions()), NullLogger<WeatherService>.Instance);
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
            .Returns(ForecastFetchResult.NotFound());

        (await h.Service.GetForecastAsync(Coord)).ShouldBeNull();

        // The 404 is remembered so the next request skips NWS.
        await h.NegativeCache.Received(1).UpsertAsync(
            Grid, Arg.Any<CachedForecastNotFound>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastNotFoundCachesTheProblemDetail()
    {
        var h = new Harness();
        var problem = new NwsProblem(
            "https://api.weather.gov/problems/MarineForecastNotSupported",
            "Marine Forecast Not Supported", 404,
            "Forecasts for marine areas are not yet supported by this API.", "1d604a85");

        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.NotFound(problem));

        (await h.Service.GetForecastAsync(Coord)).ShouldBeNull();

        await h.NegativeCache.Received(1).UpsertAsync(
            Grid,
            Arg.Is<CachedForecastNotFound>(e => e.Problem != null && e.Problem.TypeName == "MarineForecastNotSupported"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastWithFreshNotFoundTombstoneSkipsNws()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        // A fresh negative entry: the cell is known to have no forecast.
        h.NegativeCache.GetAsync(Grid, Arg.Any<CancellationToken>())
            .Returns(new CachedForecastNotFound(null, Now, Now.AddHours(6)));

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldBeNull();
        // The whole point: a fresh tombstone means ZERO calls to /forecast.
        await h.Nws.DidNotReceive().GetForecastAsync(
            Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastWithExpiredNotFoundTombstoneRefetches()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        // An EXPIRED tombstone must not suppress a re-check.
        h.NegativeCache.GetAsync(Grid, Arg.Any<CancellationToken>())
            .Returns(new CachedForecastNotFound(null, Now.AddHours(-12), Now.AddHours(-6)));
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.Success(ForecastFor(Grid), "\"e\"", TimeSpan.FromMinutes(30)));

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        await h.Nws.Received(1).GetForecastAsync(Grid, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        // A now-covered cell has its stale negative entry cleared.
        await h.NegativeCache.Received(1).RemoveAsync(Grid, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastSuccessClearsAnyNegativeTombstone()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.Success(ForecastFor(Grid), "\"e\"", TimeSpan.FromMinutes(30)));

        await h.Service.GetForecastAsync(Coord);

        await h.NegativeCache.Received(1).RemoveAsync(Grid, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastNotFoundCachingCanBeDisabledByZeroTtl()
    {
        var h = new Harness(new NwsClientOptions { NotFoundForecastTtl = TimeSpan.Zero });
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.NotFound());

        (await h.Service.GetForecastAsync(Coord)).ShouldBeNull();

        // With negative caching off, nothing is written.
        await h.NegativeCache.DidNotReceive().UpsertAsync(
            Arg.Any<GridPoint>(), Arg.Any<CachedForecastNotFound>(), Arg.Any<CancellationToken>());
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
