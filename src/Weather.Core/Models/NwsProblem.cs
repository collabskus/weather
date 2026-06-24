namespace Weather.Core.Models;

/// <summary>
/// The RFC 7807 <c>application/problem+json</c> document the National Weather
/// Service returns on a 4xx. The forecast endpoint uses it to explain a 404 —
/// most notably <c>MarineForecastNotSupported</c> for coastal/marine grid cells
/// that have no land forecast. Capturing the reason lets the negative cache log
/// and surface <em>why</em> a cell is uncovered rather than just that it is.
///
/// <para>
/// Every field is nullable because the body is outside our control: a defensive
/// parse that tolerates a missing field (or a non-JSON error page) is safer
/// than one that throws.
/// </para>
/// </summary>
public sealed record NwsProblem(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? CorrelationId)
{
    /// <summary>
    /// The short, human-meaningful problem name: the last path segment of
    /// <see cref="Type"/>. For
    /// <c>https://api.weather.gov/problems/MarineForecastNotSupported</c> this is
    /// <c>MarineForecastNotSupported</c>. Used as a low-cardinality metric tag.
    /// Returns <c>null</c> when no type URI was supplied.
    /// </summary>
    public string? TypeName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Type))
            {
                return null;
            }

            var trimmed = Type.TrimEnd('/');
            var slash = trimmed.LastIndexOf('/');
            return slash >= 0 && slash < trimmed.Length - 1
                ? trimmed[(slash + 1)..]
                : trimmed;
        }
    }
}
