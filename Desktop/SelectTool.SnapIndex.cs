using System.Numerics;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// Gathers the snap points of the pieces around the selection (not in it, and within 60 m of where it is now), each
    /// filed under its metre cell (<see cref="SnapCell"/>), so the nearest can be looked for in the cells about a point.
    /// </summary>
    /// <param name="m">The move; its Others and OthersAt are replaced.</param>
    /// <param name="cx">The selection's middle x now.</param>
    /// <param name="cz">The selection's middle z now.</param>
    internal void GatherOthers(Move m, float cx, float cz)
    {
        var s = Scene!;
        var sel = m.Items.Select(it => it.Index).ToHashSet();
        m.Others = [];
        m.OthersAt = [];
        (m.Ox, m.Oz) = (cx, cz);
        for (int i = 0; i < s.Things.Count; i++)
        {
            var t = s.Things[i];
            if (t.Gone || sel.Contains(i) || PieceCatalog.Get(t.Prefab) is not { Snaps.Length: > 0 } info)
            {
                continue;
            }
            if (MathF.Sqrt(MathF.Pow(t.Position.X - cx, 2) + MathF.Pow(t.Position.Z - cz, 2)) > 60)
            {
                continue;
            }
            foreach (var p in SnapsAt(t.Prefab, t.Position, t.Rotation))
            {
                var cell = SnapCell(p);
                (m.OthersAt.TryGetValue(cell, out var at) ? at : m.OthersAt[cell] = []).Add(m.Others.Count);
                m.Others.Add((p, info.Name));
            }
        }
    }

    /// <summary>The metre cell (x, z) a snap point is in, for <see cref="Move.OthersAt"/>.</summary>
    internal static (int X, int Z) SnapCell(Vector3 p) => ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Z));
}
