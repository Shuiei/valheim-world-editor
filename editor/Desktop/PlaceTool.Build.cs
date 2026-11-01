using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// Placing one piece or object at the cursor.
public sealed partial class PlaceTool
{
    /// <summary>
    /// Building (the Workshop): one piece where the game's hammer puts it (Hammer): on what the build ray hits, turned
    /// by Rotation, lifted by HeightNudge, snapped (SnapTo, SnapMode: by hand, only its snap point SnapPoint). On the
    /// ground, the Grid can round where the ray meets it (from the area's middle) first; with Alt held (Fine), the point
    /// is rounded to a quarter metre instead and nothing snaps.
    /// </summary>
    private Placement? BuildAt(WorldScene s, string name)
    {
        if (AimRay is not var (o, d, groundT))
        {
            return null;
        }
        float ox = s.OriginX, oz = s.OriginZ;
        var pieces = new List<Hammer.Placed>();
        lock (s.Things)
        {
            for (int i = 0; i < s.Things.Count; i++)
            {
                var t = s.Things[i];
                if (!t.Gone && NameOf(t.Prefab) is string n && Hammer.Get(n) != null)
                {
                    pieces.Add(new Hammer.Placed(i, n, t.Position, BlueprintFormats.FromEuler(t.Rotation)));
                }
            }
        }
        if (Hammer.Ray(o, d, pieces, groundT, CutY ?? float.MaxValue) is not { } hit)
        {
            return null;
        }
        float mx = ox + (s.W - 1) / 2f, mz = oz + (s.H - 1) / 2f;
        static float Round(float v, float m, float step) => m + MathF.Round((v - m) / step) * step;
        // The grid rounds on the ground outside only (in a dungeon the ray meets a room's floor).
        if (hit.Piece == null && GridStep > 0 && !Fine && hit.Point.Y < GlView.InteriorHeight)
        {
            var gp = new Vector3(Round(hit.Point.X, mx, GridStep), 0, Round(hit.Point.Z, mz, GridStep));
            gp.Y = HeightAt(s, new Vector2(gp.X - ox, gp.Z - oz));
            hit = hit with { Point = gp };
        }
        // Alt: a fine step across, on the ground or a piece alike (the height where the ray meets it).
        if (Fine)
        {
            hit = hit with { Point = new Vector3(Round(hit.Point.X, mx, FineStep), hit.Point.Y, Round(hit.Point.Z, mz, FineStep)) };
        }
        float yaw = (Rotation % 360 + 360) % 360;
        var rotation = BlueprintFormats.FromEuler(new Vector3(0, yaw, 0));
        bool manual = SnapMode == SnapModes.Manual && !Fine;
        var (pos, to) = Hammer.Place(name, rotation, hit, pieces, HeightNudge, SnapTo && SnapMode != SnapModes.Free && !Fine, manual ? SnapPoint : null);
        int count = Hammer.SnapCount(name);
        ActiveSnap = manual && count > 0 && Hammer.SnapPointAt(name, rotation, pos, SnapPoint) is { } at ? (at, (SnapPoint % count + count) % count + 1, count) : null;
        var g = new Vector2(pos.X - ox, pos.Z - oz);
        if (g.X < 1 || g.Y < 1 || g.X > s.W - 2 || g.Y > s.H - 2)
        {
            return null;
        }
        Snapped = to is int k && NameOf(s.Things[k].Prefab) is string other ? (SnapWays.To, PieceCost.PieceName(other)) : null;
        return new Placement(name, pos, new Vector3(0, yaw, 0), 0, g);
    }
}
