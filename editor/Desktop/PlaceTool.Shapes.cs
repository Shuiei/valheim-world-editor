
namespace TerrainEditor.Desktop;

// The placements of the line, grid and zone shapes.
public sealed partial class PlaceTool
{
    /// <summary>
    /// What the line, grid or zone would place (Zone: needs 3 points). Also works out <see cref="SnapGap"/>.
    /// </summary>
    /// <summary>
    /// The placements of the shape drawn: the zone's spots, the line's objects (or its pieces end to end), or the grid's
    /// cells. Nothing while the shape is not drawn.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="names">The kinds, used in turn.</param>
    /// <param name="hash">What stands already, for the spacing.</param>
    /// <param name="mask">The mask, or null.</param>
    /// <param name="others">Pieces already there to start from (null: no snapping).</param>
    private List<Placement> ShapePlacements(WorldScene s, IReadOnlyList<string> names, Hash hash, Func<int, float>? mask, List<Other>? others)
    {
        var outp = new List<Placement>();
        SnapGap = 0;
        if (Mode == Modes.Zone)
        {
            ZonePlacements(s, names, hash, mask, outp);
        }
        else if (Mode == Modes.Line && EndToEnd)
        {
            EndToEndPlacements(s, names, mask, SnapTo ? others : null, outp);
        }
        else if (Mode == Modes.Line)
        {
            LinePlacements(s, names, hash, mask, outp);
        }
        else if (Mode == Modes.Grid && GridA is { } ga && GridB is { } gb)
        {
            GridPlacements(s, names, hash, mask, ga, gb, outp);
        }
        return outp;
    }

    /// <summary>The line's pieces laid end to end along each run, from the kinds that have ends (none without such a kind).</summary>
    internal void EndToEndPlacements(WorldScene s, IReadOnlyList<string> names, Func<int, float>? mask, List<Other>? others, List<Placement> outp)
    {
        var kinds = names.Where(n => EndsOf(n) != null).ToList();
        if (kinds.Count == 0)
        {
            return;
        }
        int k = 0;
        foreach (var run in LineRuns())
        {
            k = SnappedRun(s, run, kinds, k, outp, mask, others);
        }
    }
}
