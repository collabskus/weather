namespace Weather.Core.Models;

/// <summary>
/// A National Weather Service forecast grid cell, identified by the forecast
/// office (a.k.a. <c>gridId</c>/<c>cwa</c>) and the X/Y indices within it.
/// </summary>
public readonly record struct GridPoint(string GridId, int GridX, int GridY)
{
    public override string ToString() => $"{GridId}/{GridX},{GridY}";
}
