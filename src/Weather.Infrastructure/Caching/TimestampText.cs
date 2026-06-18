using System.Globalization;

namespace Weather.Infrastructure.Caching;

/// <summary>
/// SQLite has no native <see cref="DateTimeOffset"/> type, so timestamps are
/// persisted as ISO-8601 round-trip ("O") strings and parsed back with the
/// invariant culture. Centralised here so write and read can never drift.
/// </summary>
internal static class TimestampText
{
    public static string ToText(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset Parse(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
