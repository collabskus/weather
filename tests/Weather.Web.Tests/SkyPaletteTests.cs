namespace Weather.Web.Tests;

public sealed class SkyPaletteTests
{
    [Test]
    [Arguments("Sunny", true, "Clear")]
    [Arguments("Mostly Sunny", true, "Clear")]
    [Arguments("Clear", false, "Clear")]
    [Arguments("Fair", true, "Clear")]
    [Arguments("Partly Cloudy", true, "Cloudy")]
    [Arguments("Mostly Cloudy", true, "Cloudy")]
    [Arguments("Cloudy", true, "Cloudy")]
    [Arguments("Overcast", false, "Cloudy")]
    [Arguments("Chance Showers And Thunderstorms", true, "Storms")]
    [Arguments("Showers And Thunderstorms", false, "Storms")]
    [Arguments("Severe Tstorms", true, "Storms")]
    [Arguments("Rain Showers", true, "Rain")]
    [Arguments("Light Rain", false, "Rain")]
    [Arguments("Drizzle", true, "Rain")]
    [Arguments("Patchy Fog", true, "Fog")]
    [Arguments("Areas Of Fog", false, "Fog")]
    [Arguments("Haze", true, "Fog")]
    [Arguments("Snow", true, "Snow")]
    [Arguments("Chance Snow Showers", false, "Snow")]
    [Arguments("Wintry Mix", true, "Snow")]
    public void ForPeriodMapsShortForecastToASkyState(string shortForecast, bool isDaytime, string expectedLabel)
    {
        var theme = SkyPalette.ForPeriod(isDaytime, shortForecast);

        theme.Label.ShouldBe(expectedLabel);
    }

    [Test]
    public void ForPeriodIsCaseInsensitive()
    {
        SkyPalette.ForPeriod(true, "RAIN").Label.ShouldBe("Rain");
        SkyPalette.ForPeriod(true, "sUnNy").Label.ShouldBe("Clear");
    }

    [Test]
    [Arguments((string?)null)]
    [Arguments("")]
    [Arguments("   ")]
    public void ForPeriodDefaultsToClearWhenTextIsMissing(string? shortForecast) =>
        SkyPalette.ForPeriod(true, shortForecast).Label.ShouldBe("Clear");

    [Test]
    public void ForPeriodAlwaysProducesNonEmptyColours()
    {
        var theme = SkyPalette.ForPeriod(true, "Sunny");

        theme.From.ShouldNotBeNullOrWhiteSpace();
        theme.To.ShouldNotBeNullOrWhiteSpace();
        theme.Foreground.ShouldNotBeNullOrWhiteSpace();
        theme.Accent.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public void ForPeriodUsesDistinctGradientsForDayAndNight()
    {
        var day = SkyPalette.ForPeriod(true, "Clear");
        var night = SkyPalette.ForPeriod(false, "Clear");

        day.From.ShouldNotBe(night.From);
    }

    [Test]
    public void ThunderstormsTakePrecedenceOverRainKeywords()
    {
        // "Showers And Thunderstorms" contains both "shower" and "thunder";
        // the stormier reading must win.
        SkyPalette.ForPeriod(true, "Showers And Thunderstorms").Label.ShouldBe("Storms");
    }

    [Test]
    public void SnowTakesPrecedenceOverShowerKeywords()
    {
        SkyPalette.ForPeriod(false, "Snow Showers").Label.ShouldBe("Snow");
    }
}
