namespace TerrainEditor.Editing;

/// <summary>
/// The weighted random choice the dungeon generators share.
/// </summary>
internal static class Weighted
{
    /// <summary>
    /// The item a random draw falls on, each taking as much of the range as its weight. The last item when float
    /// rounding leaves a little of the draw over (a draw close to 1), never none.
    /// </summary>
    /// <param name="items">The items to choose from; at least one.</param>
    /// <param name="weight">Each item's weight.</param>
    /// <param name="draw">A random number from 0 to 1 (Random.NextDouble).</param>
    public static T Pick<T>(IReadOnlyList<T> items, Func<T, float> weight, double draw)
    {
        float total = items.Sum(weight), roll = (float)draw * total;
        foreach (T item in items)
        {
            if ((roll -= weight(item)) <= 0)
            {
                return item;
            }
        }
        return items[^1];
    }
}
