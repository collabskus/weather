using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Weather.Web.Components.Pages;
using Weather.Web.Tests.Fakes;
using TestContext = Bunit.TestContext;

namespace Weather.Web.Tests;

public sealed class HomePageTests
{
    private static ForecastPeriodDto Period(int number, string name, bool day, int temp, int? pop, string short_) =>
        new(number, name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            day, temp, "F", pop, "5 mph", "S", short_, $"{short_}.", "icon");

    private static CellDto SamplePrimary() => new(
        "AKQ", 83, 61, 37.0900, -76.4500, null,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        Periods: new[]
        {
            Period(1, "This Afternoon", true, 90, 3, "Sunny"),
            Period(2, "Tonight", false, 74, null, "Mostly Clear"),
        },
        Hourly: new[]
        {
            Period(1, string.Empty, true, 90, 2, "Sunny"),
            Period(2, string.Empty, true, 89, 2, "Sunny"),
        },
        Observation: new ObservationDto(
            "KPHF", "Newport News", DateTimeOffset.UtcNow, "Sunny", "icon-obs",
            88, 70, 55, 8, 14, "S", 30.05, 10.0));

    private static CellDto SampleNeighbor() => new(
        "AKQ", 84, 61, 37.1100, -76.4500, 2400,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        Periods: new[] { Period(1, "This Afternoon", true, 89, 4, "Sunny") },
        Hourly: Array.Empty<ForecastPeriodDto>(),
        Observation: null);

    private static AreaDto SampleArea() => new(
        37.0879, -76.4505, "Bethel Manor", "VA", "America/New_York", "KAKQ",
        Primary: SamplePrimary(),
        Neighbors: new[] { SampleNeighbor() },
        Alerts: Array.Empty<AlertDto>());

    private static GeolocationResult Success() =>
        new(true, 37.0879, -76.4505, 12, GeolocationError.None);

    private static TestContext CreateContext(IGeolocationService geo, IWeatherApiClient api)
    {
        var ctx = new TestContext();
        ctx.Services.AddSingleton(geo);
        ctx.Services.AddSingleton(api);
        return ctx;
    }

    [Test]
    public void WhenPermissionDeniedTheManualEntryFormIsShown()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(GeolocationResult.Failed(GeolocationError.PermissionDenied)),
            FakeWeatherApiClient.Returning(SampleArea()));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=state-denied]"));
        cut.FindAll("[data-testid=manual-entry]").ShouldNotBeEmpty();
    }

    [Test]
    public void WhenPositionUnavailableTheManualEntryFormIsShown()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(GeolocationResult.Failed(GeolocationError.PositionUnavailable)),
            FakeWeatherApiClient.Returning(SampleArea()));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=state-unavailable]"));
    }

    [Test]
    public void WhenLocationAndApiSucceedTheForecastIsRendered()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(Success()),
            FakeWeatherApiClient.Returning(SampleArea()));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=forecast]"));
        cut.Find("[data-testid=hero-temp]").TextContent.ShouldContain("90");
        cut.Markup.ShouldContain("AKQ");
    }

    [Test]
    public void TheForecastRendersHourlyObservationAndNeighbourTiles()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(Success()),
            FakeWeatherApiClient.Returning(SampleArea()));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=forecast]"));
        cut.FindAll("[data-testid=observation]").ShouldNotBeEmpty();
        cut.FindAll("[data-testid=hourly]").ShouldNotBeEmpty();
        cut.FindAll("[data-testid=neighbors]").ShouldNotBeEmpty();
        cut.FindAll("[data-testid=neighbor-tile]").Count.ShouldBe(1);
    }

    [Test]
    public void WhenTheApiReportsNoCoverageTheNotCoveredStateIsShown()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(Success()),
            FakeWeatherApiClient.Returning(null));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=state-not-covered]"));
    }

    [Test]
    public void WhenTheApiThrowsTheErrorStateIsShown()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(Success()),
            FakeWeatherApiClient.Throwing(new HttpRequestException("boom")));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=state-error]"));
    }

    [Test]
    public void WhileGeolocationIsPendingTheLocatingStateIsShownThenResolves()
    {
        var (geo, gate) = FakeGeolocationService.Gated();
        using var ctx = CreateContext(geo, FakeWeatherApiClient.Returning(SampleArea()));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=state-locating]"));

        gate.SetResult(Success());
        cut.WaitForAssertion(() => cut.Find("[data-testid=forecast]"));
    }

    [Test]
    public void ManualSubmissionLoadsTheForecast()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(GeolocationResult.Failed(GeolocationError.PermissionDenied)),
            FakeWeatherApiClient.Returning(SampleArea()));

        var cut = ctx.RenderComponent<Home>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=manual-entry]"));

        cut.FindAll("[data-testid=manual-entry] input")[0].Input("37.0879");
        cut.FindAll("[data-testid=manual-entry] input")[1].Input("-76.4505");
        cut.FindAll("[data-testid=manual-entry] button")[0].Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid=forecast]"));
    }

    [Test]
    public void UseSampleButtonRequestsTheSampleCoordinates()
    {
        var api = FakeWeatherApiClient.Returning(SampleArea());
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(GeolocationResult.Failed(GeolocationError.PermissionDenied)),
            api);

        var cut = ctx.RenderComponent<Home>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=manual-entry]"));

        cut.FindAll("[data-testid=manual-entry] button")[1].Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid=forecast]"));
        api.LastLatitude.ShouldBe(37.0879);
        api.LastLongitude.ShouldBe(-76.4505);
    }
}
