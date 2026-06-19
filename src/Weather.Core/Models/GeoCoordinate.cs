using System.Globalization;

namespace Weather.Core.Models;

/// <summary>
/// A validated WGS-84 latitude/longitude pair.
/// </summary>
public readonly record struct GeoCoordinate
{
    public double Latitude { get; }
    public double Longitude { get; }

    public GeoCoordinate(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude), latitude, "Latitude must be between -90 and 90 degrees.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude), longitude, "Longitude must be between -180 and 180 degrees.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>Validation that never throws — handy for guarding API input.</summary>
    public static bool IsValid(double latitude, double longitude) =>
        !double.IsNaN(latitude) && latitude is >= -90 and <= 90 &&
        !double.IsNaN(longitude) && longitude is >= -180 and <= 180;

    /// <summary>
    /// The NWS API rounds coordinates to four decimal places (~11 m). Rounding
    /// here means many physically-close users collapse onto a single cached
    /// point-&gt;grid mapping, sharply reducing calls to <c>/points</c>.
    /// </summary>
    public GeoCoordinate Rounded(int decimals = 4) =>
        new(Math.Round(Latitude, decimals, MidpointRounding.AwayFromZero),
            Math.Round(Longitude, decimals, MidpointRounding.AwayFromZero));

    /// <summary>Invariant "lat,lon" string in the exact shape NWS expects.</summary>
    public string ToApiString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Latitude:0.####},{Longitude:0.####}");

    /// <summary>Stable cache key (identical to the API string).</summary>
    public string ToCacheKey() => ToApiString();

    /// <summary>
    /// Great-circle (haversine) distance in metres to another coordinate. Used
    /// to order neighbouring grid cells by how close they actually are to the
    /// user, since a user is rarely at the centre of their own cell.
    /// </summary>
    public double DistanceMetersTo(GeoCoordinate other)
    {
        const double earthRadiusMeters = 6_371_000d;
        const double degToRad = Math.PI / 180d;

        var lat1 = Latitude * degToRad;
        var lat2 = other.Latitude * degToRad;
        var dLat = (other.Latitude - Latitude) * degToRad;
        var dLon = (other.Longitude - Longitude) * degToRad;

        var sinLat = Math.Sin(dLat / 2);
        var sinLon = Math.Sin(dLon / 2);
        var a = (sinLat * sinLat) + (Math.Cos(lat1) * Math.Cos(lat2) * sinLon * sinLon);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    public override string ToString() => ToApiString();
}
