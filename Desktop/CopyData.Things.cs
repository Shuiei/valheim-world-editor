using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>The copy of a Select selection (CopyData.Things.cs).</summary>
public sealed partial class CopyData
{
    /// <summary>
    /// The Select tool's copy: trees and the like each keep their height above the ground where they land; building
    /// pieces keep the building's shape instead, with the ground they stand on (Footprint, without paint). Null when
    /// none can be copied.
    /// </summary>
    /// <param name="s">The scene the objects are in.</param>
    /// <param name="things">The objects, as indices into the scene's things.</param>
    /// <param name="nameOf">A prefab hash's name, null when unknown.</param>
    /// <param name="groundAt">The ground's height at a world point (x, z).</param>
    public static CopyData? FromThings(WorldScene s, IReadOnlyList<int> things, Func<int, string?> nameOf, Func<float, float, float> groundAt)
    {
        // A linked group's parts go with it (ObjectLinks: a ship), as one block; it takes no ground (it does not stand on it).
        var (chosen, parts, _, _) = s.Session?.WithLinked(things) ?? ([.. things], [], 0, LinkRule.DefaultName);
        var shown = chosen.Where(i => !s.Things[i].Gone && (s.World == null || s.World.CanCreate(s.Things[i].Prefab))).ToList();
        var list = shown.Select(i => s.Things[i]).ToList();
        if (list.Count == 0)
        {
            return null;
        }
        var grounded = shown.Where(i => !parts.Contains(i)).Select(i => s.Things[i]).ToList();
        // The ground goes on whole metres: the grid around the pieces, its middle on a metre or half (none without pieces).
        var ext = grounded.Select(t => Extent(t.Prefab, t.Position, t.Rotation, t.Scale)).OfType<(float X0, float Z0, float X1, float Z1, float Bottom)>().ToList();
        var (bx0, bx1, bz0, bz1) = GroundBox(ext, Margin + Blend);
        float cx = ext.Count == 0 ? list.Average(t => t.Position.X) : (bx0 + bx1) / 2f, cz = ext.Count == 0 ? list.Average(t => t.Position.Z) : (bz0 + bz1) / 2f;
        float x0 = list.Min(t => t.Position.X) - cx - 1, x1 = list.Max(t => t.Position.X) - cx + 1, z0 = list.Min(t => t.Position.Z) - cz - 1, z1 = list.Max(t => t.Position.Z) - cz + 1;
        if (ext.Count > 0)
        {
            (x0, x1, z0, z1) = (MathF.Min(x0, bx0 - cx), MathF.Max(x1, bx1 - cx), MathF.Min(z0, bz0 - cz), MathF.Max(z1, bz1 - cz));
        }
        // Trees, rocks and the like keep their height above the ground where each lands; building pieces
        // keep the building's shape: their height from the ground at the selection's middle (pieces
        // following the ground one by one would come apart on a slope).
        float mid = groundAt(cx, cz);
        var objs = shown.Select(i =>
        {
            var t = s.Things[i];
            bool piece = t.Piece || PieceCatalog.Get(t.Prefab) != null || parts.Contains(i);
            var (source, data) = CopySourceOf(s, t);
            return new Obj(t.Prefab, nameOf(t.Prefab) ?? t.Prefab.ToString(), t.Position.X - cx, t.Position.Z - cz,
                t.Position.Y - (piece ? mid : groundAt(t.Position.X, t.Position.Z)), t.Rotation, t.Scale, source, Follow: !piece, Data: data);
        }).ToList();
        int w = bx1 - bx0 + 1, h = bz1 - bz0 + 1;
        float[] foot = ext.Count == 0 ? [0f] : Footprint(grounded, w, h, bx0, bz0, groundAt);
        return new CopyData
        {
            W = w,
            H = h,
            Rel = [.. foot.Select((k, i) => k > 0 ? groundAt(bx0 + i % w, bz0 + i / w) - mid : float.NaN)],
            Wt = foot,
            Pnt = [.. Enumerable.Repeat(-1f, w * h * 4)],
            Objects = objs,
            Poly = [new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)],
            Taken = shown,
        };
    }
}
