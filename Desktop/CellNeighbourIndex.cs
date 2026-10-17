namespace TerrainEditor.Desktop;

/// <summary>
/// Items put once in square cells of a fixed width, so a point's nearby items are found by looking only at the cell it
/// is in and the eight cells around it. The width must be at least the farthest distance a test can reach.
/// </summary>
/// <typeparam name="T">The item kept at each position.</typeparam>
internal sealed class CellNeighbourIndex<T>
{
    private readonly float _cellWidth;
    private readonly Dictionary<(int, int), List<T>> _cells = [];

    /// <summary>Starts an empty index.</summary>
    /// <param name="cellWidth">The width of a cell, in the same units as the positions.</param>
    public CellNeighbourIndex(float cellWidth) => _cellWidth = cellWidth;

    /// <summary>Puts an item at a position.</summary>
    /// <param name="x">The item's x position.</param>
    /// <param name="z">The item's z position.</param>
    /// <param name="item">The item kept there.</param>
    public void Add(float x, float z, T item)
    {
        var key = CellOf(x, z);
        if (!_cells.TryGetValue(key, out var list))
        {
            _cells[key] = list = [];
        }
        list.Add(item);
    }

    /// <summary>Whether any item in the cell of a point or the eight around it satisfies a test.</summary>
    /// <param name="x">The point's x position.</param>
    /// <param name="z">The point's z position.</param>
    /// <param name="test">The test each nearby item is checked with; the test decides the distance.</param>
    /// <returns>True when one nearby item passes the test.</returns>
    public bool AnyNear(float x, float z, Func<T, bool> test)
    {
        var (cx, cz) = CellOf(x, z);
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (_cells.TryGetValue((cx + dx, cz + dz), out var list) && list.Exists(item => test(item)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private (int, int) CellOf(float x, float z) => ((int)MathF.Floor(x / _cellWidth), (int)MathF.Floor(z / _cellWidth));
}
