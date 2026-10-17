using System.Numerics;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The pieces already there that a placed piece can snap to, and the nearest snap point.
public sealed partial class PlaceTool
{
    /// <summary>A piece already in the block, for snapping.</summary>
    /// <param name="Index">Its index in the scene's things.</param>
    /// <param name="Name">Its prefab name.</param>
    /// <param name="G">Its grid position.</param>
    /// <param name="Rotation">Its Unity Euler rotation, in degrees.</param>
    /// <param name="Pts">Its snap points (grid x, height, grid z).</param>
    /// <param name="Low">The height of its lowest snap point.</param>
    private sealed record Other(int Index, string Name, Vector2 G, Vector3 Rotation, List<Vector3> Pts, float Low);

    /// <summary>The snap points of every piece in the block.</summary>
    private static List<Other> OtherSnaps(WorldScene s, Func<int, string?> nameOf)
    {
        var list = new List<Other>();
        float ox = s.OriginX, oz = s.OriginZ;
        lock (s.Things)
        {
            for (int i = 0; i < s.Things.Count; i++)
            {
                var t = s.Things[i];
                if (t.Gone || PieceCatalog.Get(t.Prefab) is not { Snaps.Length: > 0 } info)
                {
                    continue;
                }
                var g = new Vector2(t.Position.X - ox, t.Position.Z - oz);
                var pts = info.Snaps.Select(p => { var q = RotU(new Vector3(p[0], p[1], p[2]), t.Rotation); return new Vector3(g.X + q.X, t.Position.Y + q.Y, g.Y + q.Z); }).ToList();
                list.Add(new Other(i, nameOf(t.Prefab) ?? info.Name, g, t.Rotation, pts, pts.Min(p => p.Y)));
            }
        }
        return list;
    }

    /// <summary>A prefab hash's name (null when unknown); set by the window.</summary>
    public Func<int, string?> NameOf { get; set; } = _ => null;

    /// <summary>The snap point of a piece already there nearest to p (along the ground), within d metres.</summary>
    private static Vector3? NearestSnap(List<Other> others, Vector2 p, float d)
    {
        Vector3? best = null;
        float bd = d;
        foreach (var o in others)
        {
            if (MathF.Abs(o.G.X - p.X) > d + 8 || MathF.Abs(o.G.Y - p.Y) > d + 8)
            {
                continue;
            }
            foreach (var q in o.Pts)
            {
                float dd = Vector2.Distance(new Vector2(q.X, q.Z), p);
                if (dd < bd)
                {
                    bd = dd;
                    best = q;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Snapping a single piece to the pieces already there: beside one (end to end, at its level) or on top of the one
    /// under the cursor; its turn follows that piece (in quarter turns). What the last preview snapped to: how, and the
    /// other piece's name; null when nothing.
    /// </summary>
    public (SnapWays Way, string Name)? Snapped { get; private set; }

    /// <summary>How the last preview's piece snapped to another.</summary>
    public enum SnapWays
    {
        /// <summary>On top of the one under the cursor.</summary>
        OnTop,
        /// <summary>Beside the nearest one, end to end.</summary>
        Beside,
        /// <summary>To a snap point of another, as the hammer does (building).</summary>
        To,
    }
}
