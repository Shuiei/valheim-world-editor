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
    /// by Rotation, lifted by HeightNudge, snapped (SnapTo). On the ground, the Grid can round where the ray meets it
    /// (from the area's middle) first.
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
        // The grid rounds on the ground outside only (in a dungeon the ray meets a room's floor).
        if (hit.Piece == null && GridStep > 0 && hit.Point.Y < GlView.InteriorHeight)
        {
            float mx = ox + (s.W - 1) / 2f, mz = oz + (s.H - 1) / 2f;
            var gp = new Vector3(mx + MathF.Round((hit.Point.X - mx) / GridStep) * GridStep, 0, mz + MathF.Round((hit.Point.Z - mz) / GridStep) * GridStep);
            gp.Y = HeightAt(s, new Vector2(gp.X - ox, gp.Z - oz));
            hit = hit with { Point = gp };
        }
        float yaw = (Rotation % 360 + 360) % 360;
        var (pos, to) = Hammer.Place(name, BlueprintFormats.FromEuler(new Vector3(0, yaw, 0)), hit, pieces, HeightNudge, SnapTo);
        var g = new Vector2(pos.X - ox, pos.Z - oz);
        if (g.X < 1 || g.Y < 1 || g.X > s.W - 2 || g.Y > s.H - 2)
        {
            return null;
        }
        Snapped = to is int k && NameOf(s.Things[k].Prefab) is string other ? (SnapWays.To, PieceCost.PieceName(other)) : null;
        return new Placement(name, pos, new Vector3(0, yaw, 0), 0, g);
    }
}
