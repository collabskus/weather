using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Weather.Core.Abstractions;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Services;
using Weather.Infrastructure.Tests.Fakes;

namespace Weather.Infrastructure.Tests.Services;

public sealed class NeighborhoodWarmerTests
{
    private static readonly GridPoint Origin = new("AKQ", 83, 61);
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task RequestWarming_enqueues_the_origin_for_the_reader()
    {
        var warmer = new NeighborhoodWarmer(NullLogger<NeighborhoodWarmer>.Instance);

        warmer.RequestWarming(Origin);

        (await warmer.Reader.ReadAsync()).ShouldBe(Origin);
    }

    [Test]
    public async Task BackgroundService_warms_all_eight_neighbours()
    {
        var bag = new ConcurrentBag<GridPoint>();
        var fakeWeather = Substitute.For<IWeatherService>();
        fakeWeather.GetForecastByGridAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                bag.Add(ci.Arg<GridPoint>());
                return (Forecast?)null;
            });

        var forecastCache = Substitute.For<IForecastCache>(); // all cells cold by default

        await RunWarmingAsync(fakeWeather, forecastCache, () => bag.Count >= 8);

        bag.Distinct().ShouldBe(GridNeighborhood.Surrounding(Origin), ignoreOrder: true);
    }

    [Test]
    public async Task BackgroundService_skips_cells_that_are_already_fresh()
    {
        var freshA = new GridPoint("AKQ", 82, 60);
        var freshB = new GridPoint("AKQ", 84, 62);

        var bag = new ConcurrentBag<GridPoint>();
        var fakeWeather = Substitute.For<IWeatherService>();
        fakeWeather.GetForecastByGridAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                bag.Add(ci.Arg<GridPoint>());
                return (Forecast?)null;
            });

        var forecastCache = Substitute.For<IForecastCache>();
        var fresh = new CachedForecast(
            new Forecast(freshA, Now, Now, Array.Empty<ForecastPeriod>()), "\"e\"", Now, Now.AddHours(1));
        forecastCache.GetAsync(freshA, Arg.Any<CancellationToken>()).Returns(fresh);
        forecastCache.GetAsync(freshB, Arg.Any<CancellationToken>()).Returns(fresh);

        // Six cold cells remain, so wait for those six fetches.
        await RunWarmingAsync(fakeWeather, forecastCache, () => bag.Count >= 6);

        bag.ShouldNotContain(freshA);
        bag.ShouldNotContain(freshB);
        bag.Distinct().Count().ShouldBe(6);
    }

    private static async Task RunWarmingAsync(
        IWeatherService fakeWeather, IForecastCache forecastCache, Func<bool> until)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => fakeWeather);
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromMilliseconds(50),
            QueueLimit = 100,
            AutoReplenishment = true,
        });

        var warmer = new NeighborhoodWarmer(NullLogger<NeighborhoodWarmer>.Instance);
        var service = new NeighborhoodWarmingBackgroundService(
            warmer, scopeFactory, forecastCache, rateLimiter,
            new WeatherTelemetry(), new MutableTimeProvider(Now),
            NullLogger<NeighborhoodWarmingBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        warmer.RequestWarming(Origin);

        await WaitUntilAsync(until, TimeSpan.FromSeconds(5));

        await service.StopAsync(CancellationToken.None);
        await rateLimiter.DisposeAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(25);
        }
    }
}
