using Weather.Infrastructure.Caching;

namespace Weather.Infrastructure.Tests.Caching;

public sealed class SqlitePointMetadataCacheTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);

    private static PointMetadata BuildMetadata(GeoCoordinate query, GridPoint grid) =>
        new(
            Query: query,
            Grid: grid,
            ForecastUrl: $"https://api.weather.gov/gridpoints/{grid.GridId}/{grid.GridX},{grid.GridY}/forecast",
            ForecastHourlyUrl: $"https://api.weather.gov/gridpoints/{grid.GridId}/{grid.GridX},{grid.GridY}/forecast/hourly",
            City: "Bethel Manor",
            State: "VA",
            TimeZone: "America/New_York",
            RadarStation: "KAKQ");

    [Test]
    public async Task UpsertThenGetRoundTripsTheMetadata()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqlitePointMetadataCache(harness.Factory);

        var coordinate = new GeoCoordinate(37.0879, -76.4505);
        var grid = new GridPoint("AKQ", 83, 61);
        await cache.UpsertAsync(new CachedPointMetadata(BuildMetadata(coordinate, grid), Now, Now.AddDays(30)));

        var loaded = await cache.GetAsync(coordinate);

        loaded.ShouldNotBeNull();
        loaded!.Metadata.Grid.ShouldBe(grid);
        loaded.Metadata.City.ShouldBe("Bethel Manor");
        loaded.Metadata.State.ShouldBe("VA");
        loaded.Metadata.TimeZone.ShouldBe("America/New_York");
        loaded.Metadata.RadarStation.ShouldBe("KAKQ");
        loaded.Metadata.ForecastUrl.ShouldBe("https://api.weather.gov/gridpoints/AKQ/83,61/forecast");
        loaded.IsFresh(Now).ShouldBeTrue();
    }

    [Test]
    public async Task NearbyCoordinatesCollapseOntoOneRow()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqlitePointMetadataCache(harness.Factory);

        // Stored against one coordinate...
        var stored = new GeoCoordinate(37.08792, -76.45048);
        var grid = new GridPoint("AKQ", 83, 61);
        await cache.UpsertAsync(new CachedPointMetadata(BuildMetadata(stored, grid), Now, Now.AddDays(30)));

        // ...and fetched with a slightly different coordinate that rounds the same.
        var lookup = new GeoCoordinate(37.08788, -76.45052);
        var loaded = await cache.GetAsync(lookup);

        loaded.ShouldNotBeNull();
        loaded!.Metadata.Grid.ShouldBe(grid);
    }

    [Test]
    public async Task GetReturnsNullForAnUnknownCoordinate()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqlitePointMetadataCache(harness.Factory);

        (await cache.GetAsync(new GeoCoordinate(40.7128, -74.0060))).ShouldBeNull();
    }

    [Test]
    public async Task UpsertOverwritesMetadataForTheSameKey()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqlitePointMetadataCache(harness.Factory);

        var coordinate = new GeoCoordinate(37.0879, -76.4505);
        await cache.UpsertAsync(new CachedPointMetadata(
            BuildMetadata(coordinate, new GridPoint("AKQ", 83, 61)), Now, Now.AddDays(30)));
        await cache.UpsertAsync(new CachedPointMetadata(
            BuildMetadata(coordinate, new GridPoint("AKQ", 84, 62)), Now, Now.AddDays(30)));

        var loaded = await cache.GetAsync(coordinate);

        loaded.ShouldNotBeNull();
        loaded!.Metadata.Grid.ShouldBe(new GridPoint("AKQ", 84, 62));
    }
}
