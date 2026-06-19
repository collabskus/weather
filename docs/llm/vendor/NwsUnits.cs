namespace Weather.Infrastructure.Nws;

/// <summary>
/// NWS observations are reported in SI units. The forecast is in US units
/// (°F, mph), so observations are converted to match for a consistent UI.
/// All helpers are null-tolerant: a missing measurement stays missing.
/// </summary>
internal static class NwsUnits
{
    public static int? CelsiusToFahrenheit(double? celsius) =>
        celsius is { } c ? (int)Math.Round((c * 9d / 5d) + 32d, MidpointRounding.AwayFromZero) : null;

    public static int? KmhToMph(double? kmh) =>
        kmh is { } k ? (int)Math.Round(k * 0.621371d, MidpointRounding.AwayFromZero) : null;

    /// <summary>NWS pressure is in pascals; convert to inches of mercury.</summary>
    public static double? PascalsToInHg(double? pascals) =>
        pascals is { } p ? Math.Round(p * 0.0002953d, 2, MidpointRounding.AwayFromZero) : null;

    /// <summary>NWS visibility is in metres; convert to statute miles.</summary>
    public static double? MetersToMiles(double? meters) =>
        meters is { } m ? Math.Round(m * 0.000621371d, 1, MidpointRounding.AwayFromZero) : null;

    public static int? RoundToInt(double? value) =>
        value is { } v ? (int)Math.Round(v, MidpointRounding.AwayFromZero) : null;

    private static readonly string[] CompassPoints =
    [
        "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
        "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW",
    ];

    /// <summary>Convert a wind bearing in degrees to a 16-point compass label.</summary>
    public static string? DegreesToCompass(double? degrees)
    {
        if (degrees is not { } d)
        {
            return null;
        }

        var normalized = ((d % 360d) + 360d) % 360d;
        var index = (int)Math.Round(normalized / 22.5d, MidpointRounding.AwayFromZero) % 16;
        return CompassPoints[index];
    }
}
