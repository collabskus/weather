namespace Weather.Web.Services;

/// <summary>
/// A resolved "sky" appearance for one forecast period: the two gradient stops,
/// a foreground colour chosen for legibility against that gradient, an accent,
/// and a short human label of the sky state.
/// </summary>
public readonly record struct SkyTheme(
    string From,
    string To,
    string Foreground,
    string Accent,
    string Label);

/// <summary>
/// The dashboard's signature element. Rather than a generic card colour, the
/// hero panel paints the actual sky implied by the forecast: the gradient is
/// derived at runtime from whether the period is day or night and from
/// keywords in its short forecast (clear, cloud, rain, storms, snow, fog).
/// Foreground colours are picked per state so text stays readable — part of the
/// accessibility floor, not an afterthought.
/// </summary>
public static class SkyPalette
{
    private const string Accent = "#FFB454"; // warm amber, shared with focus rings

    /// <summary>
    /// Resolve a <see cref="SkyTheme"/> for a period. Keyword precedence runs
    /// most-dominant first (storms beat plain rain; rain beats cloud) so a
    /// "Chance Showers And Thunderstorms" period reads as stormy, not cloudy.
    /// </summary>
    public static SkyTheme ForPeriod(bool isDaytime, string? shortForecast)
    {
        var text = (shortForecast ?? string.Empty).ToLowerInvariant();

        if (Contains(text, "thunder") || Contains(text, "tstorm") || Contains(text, "storm"))
        {
            return isDaytime
                ? new SkyTheme("#2B3A4A", "#51657A", "#FFFFFF", Accent, "Storms")
                : new SkyTheme("#0E1620", "#243140", "#EAF2FF", Accent, "Storms");
        }

        if (Contains(text, "snow") || Contains(text, "sleet") || Contains(text, "flurr") || Contains(text, "wintry") || Contains(text, "ice"))
        {
            return isDaytime
                ? new SkyTheme("#9FB4CC", "#E2ECF6", "#16202E", Accent, "Snow")
                : new SkyTheme("#2A3850", "#4A5C76", "#F0F5FB", Accent, "Snow");
        }

        if (Contains(text, "rain") || Contains(text, "shower") || Contains(text, "drizzle"))
        {
            return isDaytime
                ? new SkyTheme("#3F6079", "#7E97AB", "#FFFFFF", Accent, "Rain")
                : new SkyTheme("#16222F", "#2C3E50", "#EAF2FF", Accent, "Rain");
        }

        if (Contains(text, "fog") || Contains(text, "haze") || Contains(text, "mist") || Contains(text, "smoke"))
        {
            return isDaytime
                ? new SkyTheme("#9AA6AE", "#CDD6DC", "#1A222B", Accent, "Fog")
                : new SkyTheme("#2A3138", "#49535C", "#EDF1F4", Accent, "Fog");
        }

        if (Contains(text, "cloud") || Contains(text, "overcast"))
        {
            return isDaytime
                ? new SkyTheme("#8A9BB0", "#C9D4E0", "#1A2433", Accent, "Cloudy")
                : new SkyTheme("#20293A", "#3C4A60", "#E6ECF5", Accent, "Cloudy");
        }

        // Clear / sunny / fair, and the default when nothing matches.
        return isDaytime
            ? new SkyTheme("#4DA6FF", "#BFE3FF", "#0B1F33", Accent, "Clear")
            : new SkyTheme("#0B1F3A", "#1C3A5E", "#EAF2FF", Accent, "Clear");
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.Ordinal);
}
