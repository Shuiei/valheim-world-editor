using System.Numerics;

namespace TerrainEditor.Desktop;

// The pieces of a run: whether one is on the block, the height it stands at, and the room the pieces grow into.
public sealed partial class PlaceTool
{
    /// <summary>Whether a run's piece at (px, pz) is on the block (a metre in from the edge) and not masked out.</summary>
    internal static bool OnRunBlock(WorldScene s, float px, float pz, Func<int, float>? mask) =>
        px >= 1 && pz >= 1 && px <= s.W - 2 && pz <= s.H - 2 && (mask == null || mask((int)MathF.Round(pz) * s.W + (int)MathF.Round(px)) > 0);

    /// <summary>The height a run's piece stands at: the elevation's, the snapped piece's level, or the lowest ground under it.</summary>
    internal float RunBottom(WorldScene s, Vector2 st, Vector2 q, Vector3? start)
    {
        if (Elevation == Elevations.At)
        {
            return Elev;
        }
        if (start is { } sv && Elevation == Elevations.Ground)
        {
            return sv.Y;
        }
        return MathF.Min(MathF.Min(HeightAt(s, st), HeightAt(s, q)), HeightAt(s, (st + q) / 2)) + (Elevation == Elevations.Above ? Elev : 0);
    }

    /// <summary>Saplings and crops: the grow radius is kept between the placements themselves too.</summary>
    private List<Placement> RoomToGrow(List<Placement> list)
    {
        if (!GrowMatters || !GrowRoom || !list.Any(o => GrowNeed(o.Name) > 0))
        {
            return list;
        }
        var kept = new List<(Vector2 G, float Need)>();
        var outp = new List<Placement>();
        foreach (var o in list)
        {
            float need = GrowNeed(o.Name);
            if (kept.Any(k => Vector2.Distance(k.G, o.G) < MathF.Max(need, k.Need)))
            {
                continue;
            }
            kept.Add((o.G, need));
            outp.Add(o);
        }
        return outp;
    }
}
