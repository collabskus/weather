using System.Globalization;

namespace Weather.Core.Tests;

public sealed class GeoCoordinateTests
{
    [Test]
    public void Constructor_accepts_valid_coordinates()
    {
        var coordinate = new GeoCoordinate(37.0879, -76.4505);

        coordinate.Latitude.ShouldBe(37.0879);
        coordinate.Longitude.ShouldBe(-76.4505);
    }

    [Test]
    [Arguments(0d, 0d)]
    [Arguments(90d, 180d)]
    [Arguments(-90d, -180d)]
    public void Constructor_accepts_boundary_values(double latitude, double longitude)
    {
        var coordinate = new GeoCoordinate(latitude, longitude);

        coordinate.Latitude.ShouldBe(latitude);
        coordinate.Longitude.ShouldBe(longitude);
    }

    [Test]
    [Arguments(90.0001d, 0d)]
    [Arguments(-90.0001d, 0d)]
    public void Constructor_rejects_out_of_range_latitude(double latitude, double longitude) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoCoordinate(latitude, longitude));

    [Test]
    [Arguments(0d, 180.0001d)]
    [Arguments(0d, -180.0001d)]
    public void Constructor_rejects_out_of_range_longitude(double latitude, double longitude) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoCoordinate(latitude, longitude));

    [Test]
    public void Constructor_rejects_NaN()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoCoordinate(double.NaN, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoCoordinate(0, double.NaN));
    }

    [Test]
    [Arguments(37.0879d, -76.4505d, true)]
    [Arguments(0d, 0d, true)]
    [Arguments(90d, 180d, true)]
    [Arguments(90.5d, 0d, false)]
    [Arguments(0d, 200d, false)]
    public void IsValid_matches_constructor_acceptance(double latitude, double longitude, bool expected) =>
        GeoCoordinate.IsValid(latitude, longitude).ShouldBe(expected);

    [Test]
    public void IsValid_rejects_NaN()
    {
        GeoCoordinate.IsValid(double.NaN, 0).ShouldBeFalse();
        GeoCoordinate.IsValid(0, double.NaN).ShouldBeFalse();
    }

    [Test]
    public void Rounded_reduces_precision_to_four_decimals()
    {
        var coordinate = new GeoCoordinate(37.08789999, -76.45051234);

        var rounded = coordinate.Rounded();

        rounded.Latitude.ShouldBe(37.0879);
        rounded.Longitude.ShouldBe(-76.4505);
    }

    [Test]
    public void Rounded_collapses_nearby_coordinates_to_the_same_value()
    {
        // Two points ~5 m apart round to the same 4-dp coordinate, which is what
        // lets nearby users share one cached /points lookup.
        var a = new GeoCoordinate(37.08791, -76.45049).Rounded();
        var b = new GeoCoordinate(37.08793, -76.45051).Rounded();

        a.ShouldBe(b);
        a.ToCacheKey().ShouldBe(b.ToCacheKey());
    }

    [Test]
    public void ToApiString_uses_invariant_decimal_point()
    {
        var coordinate = new GeoCoordinate(37.0879, -76.4505);

        coordinate.ToApiString().ShouldBe("37.0879,-76.4505");
    }

    [Test]
    public void ToApiString_trims_trailing_zeros_to_four_places()
    {
        var coordinate = new GeoCoordinate(37.5, -76);

        coordinate.ToApiString().ShouldBe("37.5,-76");
    }

    [Test]
    public void ToApiString_is_culture_independent()
    {
        // Guard against a comma decimal separator leaking in under a non-US culture.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            new GeoCoordinate(37.0879, -76.4505).ToApiString().ShouldBe("37.0879,-76.4505");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public void ToCacheKey_equals_api_string() =>
        new GeoCoordinate(12.3456, -65.4321).ToCacheKey().ShouldBe("12.3456,-65.4321");
}
