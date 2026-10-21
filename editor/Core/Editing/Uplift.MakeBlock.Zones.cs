using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    /// <summary>
    /// The zones' new edits: what the discs leave, within ±8 m, with the discs' dirt painted back. Zones not generated
    /// yet keep no edit but their lift is dropped.
    /// </summary>
    /// <param name="world">The world (which zones are generated).</param>
    /// <param name="terrain">The terrain service (the base masks).</param>
    /// <param name="block">The block worked on.</param>
    /// <param name="targets">The ground asked for, the lift and the zones' edits.</param>
    /// <param name="region">The points where a disc may change the ground.</param>
    /// <param name="final">The discs' positions and heights.</param>
    /// <param name="ground">The ground the discs make.</param>
    /// <returns>The zones' edits, and the largest height still off where the lift or the region matter.</returns>
    private static (List<ZoneEdit> Zones, float Worst) BuildZoneEdits(WorldSave world, ValheimGen.TerrainService terrain, Block block, BlockTargets targets, bool[] region, List<Vector3> final, float[] ground)
    {
        float worst = 0;
        bool[] painted = new bool[block.N];
        foreach (var d in final)
        {
            block.Disc(d, PaintRadius + 1, p => painted[p] = true);
        }
        var result = new List<ZoneEdit>();
        foreach (var ((zx, zz), e) in targets.Zones)
        {
            if (!Generated(world, zx, zz))
            {
                // Nothing can be saved there: the lift is just dropped.
                if (e.HasLift)
                {
                    var dropped = e.Clone();
                    Array.Clear(dropped.Lift);
                    result.Add(dropped);
                }
                continue;
            }
            float[] mask = terrain.BaseMask(zx, zz);
            var n = new ZoneEdit(zx, zz);
            bool any = false;
            block.Each(zx, zz, (i, p) =>
            {
                float r = targets.Want[p] - ground[p];
                if (targets.Lift[p] != 0 || region[p])
                {
                    worst = MathF.Max(worst, MathF.Abs(r));
                }
                if (e.Modified[i] || MathF.Abs(r) > 0.01f)
                {
                    n.Modified[i] = true;
                    n.Level[i] = Math.Clamp(r, -EditStore.MaxLevel, EditStore.MaxLevel);
                }
                n.PaintModified[i] = e.PaintModified[i];
                Array.Copy(e.PaintModified[i] ? e.Paint : mask, i * 4, n.Paint, i * 4, 4);
                if (painted[p] && !e.PaintModified[i])
                {
                    n.PaintModified[i] = true;
                }
                any |= n.Modified[i] != e.Modified[i] || n.Level[i] != e.Level[i] || n.PaintModified[i] != e.PaintModified[i] || e.Lift[i] != 0;
            });
            if (any || e.HasLift)
            {
                result.Add(n);
            }
        }
        return (result, worst);
    }
}
