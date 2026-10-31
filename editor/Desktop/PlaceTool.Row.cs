using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

// Building: a row of the same piece, laid edge to edge by dragging.
public sealed partial class PlaceTool
{
    /// <summary>The most pieces one drag lays in a row.</summary>
    internal const int MaxRow = 64;

    /// <summary>
    /// Building, dragged: copies of the first piece laid edge to edge from it toward a point, along whichever of its own
    /// two sides (across or through it) is nearer the drag, one piece's length apart, at its height; a thin piece (a
    /// wall, no length through it) only along its length. The length is the distance between its outermost snap points
    /// along that side (1 m for a piece without); at most <see cref="MaxRow"/>.
    /// </summary>
    /// <param name="first">The piece the press put down (its place, turn and grid point).</param>
    /// <param name="to">Where the pointer is now, in the world.</param>
    /// <returns>The row, the first piece first; just the first while the drag is under half a piece.</returns>
    internal static List<Placement> Row(Placement first, Vector3 to)
    {
        var turn = BlueprintFormats.FromEuler(first.Rotation);
        var across = Vector3.Transform(Vector3.UnitX, turn);
        var through = Vector3.Transform(Vector3.UnitZ, turn);
        var drag = to - first.Position;
        drag.Y = 0;
        float a = Vector3.Dot(drag, across), t = Vector3.Dot(drag, through);
        float? wide = Extent(first.Name, Vector3.UnitX), deep = Extent(first.Name, Vector3.UnitZ);
        // A side the piece has no length along is no way to row it, when the other has one.
        bool alongAcross = (wide, deep) switch
        {
            (not null, null) => true,
            (null, not null) => false,
            _ => MathF.Abs(a) >= MathF.Abs(t),
        };
        var axis = (alongAcross ? across : through) * (MathF.Sign(alongAcross ? a : t) is var sign and not 0 ? sign : 1);
        float step = (alongAcross ? wide : deep) ?? 1;
        int n = Math.Min(MaxRow - 1, (int)MathF.Round(MathF.Abs(alongAcross ? a : t) / step));
        var row = new List<Placement>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            var move = axis * (step * i);
            row.Add(first with { Position = first.Position + move, G = first.G + new Vector2(move.X, move.Z) });
        }
        return row;
    }

    /// <summary>
    /// A piece's length along one of its own sides: from its outermost snap point to the other along it; 1 m when it
    /// has none apart along that side.
    /// </summary>
    /// <param name="prefab">The piece's prefab name.</param>
    /// <param name="side">The side, in its own frame (X across, Z through).</param>
    internal static float Length(string prefab, Vector3 side) => Extent(prefab, side) ?? 1;

    /// <summary>A piece's length along one of its own sides from its snap points; null when they are not 10 cm apart along it.</summary>
    /// <param name="prefab">The piece's prefab name.</param>
    /// <param name="side">The side, in its own frame (X across, Z through).</param>
    private static float? Extent(string prefab, Vector3 side)
    {
        if (Hammer.Get(prefab) is not { Snaps.Length: > 1 } data)
        {
            return null;
        }
        float min = data.Snaps.Min(p => Vector3.Dot(p, side)), max = data.Snaps.Max(p => Vector3.Dot(p, side));
        return max - min > 0.1f ? max - min : null;
    }
}
