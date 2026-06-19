namespace Weather.Core.Tests;

public sealed class GeoDistanceTests
{
    [Test]
    public void DistanceToSamePointIsZero()
    {
        var point = new GeoCoordinate(37.0879, -76.4505);

        point.DistanceMetersTo(point).ShouldBe(0d, tolerance: 0.001);
    }

    [Test]
    public void OneDegreeOfLatitudeIsRoughly111Kilometres()
    {
        var a = new GeoCoordinate(37.0, -76.0);
        var b = new GeoCoordinate(38.0, -76.0);

        // A degree of latitude is ~111.2 km anywhere on the globe.
        a.DistanceMetersTo(b).ShouldBe(111_195d, tolerance: 600d);
    }

    [Test]
    public void DistanceIsSymmetric()
    {
        var a = new GeoCoordinate(37.0879, -76.4505);
        var b = new GeoCoordinate(37.2000, -76.3000);

        a.DistanceMetersTo(b).ShouldBe(b.DistanceMetersTo(a), tolerance: 0.001);
    }

    [Test]
    public void NearbyPointsAreCloserThanDistantOnes()
    {
        var origin = new GeoCoordinate(37.0879, -76.4505);
        var near = new GeoCoordinate(37.0979, -76.4505);
        var far = new GeoCoordinate(37.5000, -76.4505);

        origin.DistanceMetersTo(near).ShouldBeLessThan(origin.DistanceMetersTo(far));
    }
}
