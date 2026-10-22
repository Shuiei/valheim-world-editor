using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>The copy of an Area selection (CopyData.Area.cs).</summary>
public sealed partial class CopyData
{
    /// <summary>
    /// The shown objects inside an Area selection, with the ground their buildings stand on (Footprint); null without
    /// a selection. As the Select tool's copy: trees and the like keep their height above the ground where they land,
    /// building pieces the building's shape (from the selection's average height).
    /// </summary>
    /// <param name="area">The selection.</param>
    /// <param name="g">The ground.</param>
    /// <param name="s">The scene the objects are in.</param>
    /// <param name="objects">The objects to copy, as indices into the scene's things (kinds the world cannot make are left out).</param>
    /// <param name="nameOf">A prefab hash's name, null when unknown.</param>
    public static CopyData? FromArea(AreaTool area, Ground g, WorldScene s, IReadOnlyList<int> objects, Func<int, string?> nameOf)
    {
        if (area.WeightsIn(g.W, g.H) is not { Cells.Count: > 0 } a)
        {
            return null;
        }
        // A linked group's parts go with it (ObjectLinks: a ship); it takes no ground (it does not stand on it).
        var (chosen, parts, _, _) = s.Session?.WithLinked(objects) ?? ([.. objects], [], 0, LinkRule.DefaultName);
        objects = chosen;
        float reference = a.Cells.Average(c => g.HeightOf(c.G));
        int w = a.X1 - a.X0 + 1, h = a.Z1 - a.Z0 + 1;
        float ox = s.OriginX, oz = s.OriginZ;
        float GroundAt(float x, float z) => g.HeightOf(Math.Clamp((int)MathF.Round(z - oz), 0, g.H - 1) * g.W + Math.Clamp((int)MathF.Round(x - ox), 0, g.W - 1));
        float[] foot = Footprint(objects.Where(i => !parts.Contains(i)).Select(i => s.Things[i]), w, h, ox + a.X0, oz + a.Z0, GroundAt);
        float[] rel = [.. Enumerable.Repeat(float.NaN, w * h)];
        float[] wt = new float[w * h];
        float[] pnt = [.. Enumerable.Repeat(-1f, w * h * 4)];
        foreach (var (p, k) in a.Cells)
        {
            int gx = p % g.W, gz = p / g.W, i = (gz - a.Z0) * w + gx - a.X0;
            if (foot[i] <= 0)
            {
                continue;
            }
            rel[i] = g.HeightOf(p) - reference;
            wt[i] = k * foot[i];
            if (g.PMod[p] != 0)
            {
                Array.Copy(g.Paint, p * 4, pnt, i * 4, 4);
            }
        }
        float cx = (a.X0 + a.X1) / 2f, cz = (a.Z0 + a.Z1) / 2f;
        var taken = objects.Where(i => s.World == null || s.World.CanCreate(s.Things[i].Prefab)).ToList();
        var objs = taken.Select(i =>
        {
            var t = s.Things[i];
            bool piece = KeepsShape(t, parts.Contains(i));
            var (source, data) = CopySourceOf(s, t);
            return new Obj(t.Prefab, nameOf(t.Prefab) ?? t.Prefab.ToString(), t.Position.X - ox - cx, t.Position.Z - oz - cz,
                t.Position.Y - (piece ? reference : GroundAt(t.Position.X, t.Position.Z)), t.Rotation, t.Scale, source, Follow: !piece, Data: data);
        }).ToList();
        return new CopyData
        {
            W = w,
            H = h,
            Rel = rel,
            Wt = wt,
            Pnt = pnt,
            Objects = objs,
            Poly = [.. a.Poly.Select(p => p - new Vector2(cx, cz))],
            Taken = taken,
            Middle = new Vector3(ox + cx, reference, oz + cz),
        };
    }
}
