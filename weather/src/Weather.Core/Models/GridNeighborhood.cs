namespace Weather.Core.Models;

/// <summary>
/// Pure geometry for the "neighbourhood" strategy: given the user's grid cell,
/// compute the surrounding cells whose data may be relevant when the user sits
/// near a cell boundary.
/// </summary>
public static class GridNeighborhood
{
    /// <summary>
    /// The cells surrounding <paramref name="origin"/> within a Chebyshev
    /// distance of <paramref name="radius"/> (default 1 ⇒ the 8 cells of a 3×3
    /// block), excluding the origin itself. Cells with negative indices are
    /// dropped because NWS grid coordinates are always non-negative.
    /// </summary>
    public static IReadOnlyList<GridPoint> Surrounding(GridPoint origin, int radius = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var result = new List<GridPoint>((((2 * radius) + 1) * ((2 * radius) + 1)) - 1);
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var x = origin.GridX + dx;
                var y = origin.GridY + dy;
                if (x < 0 || y < 0)
                {
                    continue;
                }

                result.Add(origin with { GridX = x, GridY = y });
            }
        }

        return result;
    }
}
