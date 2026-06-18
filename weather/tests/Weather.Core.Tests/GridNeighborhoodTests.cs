namespace Weather.Core.Tests;

public sealed class GridNeighborhoodTests
{
    private static readonly GridPoint Origin = new("AKQ", 83, 61);

    [Test]
    public void Surrounding_returns_eight_cells_for_radius_one()
    {
        var cells = GridNeighborhood.Surrounding(Origin);

        cells.Count.ShouldBe(8);
    }

    [Test]
    public void Surrounding_excludes_the_origin()
    {
        var cells = GridNeighborhood.Surrounding(Origin);

        cells.ShouldNotContain(Origin);
    }

    [Test]
    public void Surrounding_returns_the_expected_ring()
    {
        var cells = GridNeighborhood.Surrounding(Origin);

        var expected = new[]
        {
            new GridPoint("AKQ", 82, 60), new GridPoint("AKQ", 82, 61), new GridPoint("AKQ", 82, 62),
            new GridPoint("AKQ", 83, 60),                               new GridPoint("AKQ", 83, 62),
            new GridPoint("AKQ", 84, 60), new GridPoint("AKQ", 84, 61), new GridPoint("AKQ", 84, 62),
        };

        cells.ShouldBe(expected, ignoreOrder: true);
    }

    [Test]
    public void Surrounding_preserves_the_grid_id()
    {
        var cells = GridNeighborhood.Surrounding(Origin);

        cells.ShouldAllBe(cell => cell.GridId == "AKQ");
    }

    [Test]
    public void Surrounding_drops_cells_with_negative_indices()
    {
        // Origin in the corner: cells at x = -1 or y = -1 must be dropped.
        var corner = new GridPoint("AKQ", 0, 0);

        var cells = GridNeighborhood.Surrounding(corner);

        cells.ShouldBe(new[]
        {
            new GridPoint("AKQ", 0, 1),
            new GridPoint("AKQ", 1, 0),
            new GridPoint("AKQ", 1, 1),
        }, ignoreOrder: true);
    }

    [Test]
    public void Surrounding_with_radius_two_returns_twenty_four_cells()
    {
        var cells = GridNeighborhood.Surrounding(Origin, radius: 2);

        cells.Count.ShouldBe(24);
        cells.ShouldNotContain(Origin);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public void Surrounding_rejects_non_positive_radius(int radius) =>
        Should.Throw<ArgumentOutOfRangeException>(() => GridNeighborhood.Surrounding(Origin, radius));
}
