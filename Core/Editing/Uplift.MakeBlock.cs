using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    /// <summary>The ground asked for at each point of a block, the lift there, the zones' edits and which points are open to edits.</summary>
    /// <param name="Want">The ground asked for, per block point.</param>
    /// <param name="Lift">The lift, per block point.</param>
    /// <param name="Zones">The block's zones' edits, by zone.</param>
    /// <param name="Open">Whether a point belongs to a generated zone (so edits can be saved there).</param>
    /// <param name="Ungenerated">The lifted zones not generated yet (their lift is dropped).</param>
    private sealed record BlockTargets(float[] Want, float[] Lift, Dictionary<(int, int), ZoneEdit> Zones, bool[] Open, HashSet<(int, int)> Ungenerated);

    /// <summary>The plan for a group of lifted zones, worked on one block: them and the zones the discs reach from them.</summary>
    private static Plan MakeBlock(WorldSave world, ValheimGen.TerrainService terrain, EditStore edits, Func<int> nextId, List<ZoneEdit> lifted, Shared shared)
    {
        // The block of zones worked on: the lifted ones and those the discs reach from them.
        var block = new Block(WorkingZones(lifted.Select(e => (e.ZoneX, e.ZoneZ))));
        var targets = ReadBlockTargets(world, block, terrain, edits, lifted);

        // Old discs near the lift are worked out again with the new ones.
        bool[] liftMask = Dilate(block, targets.Lift, l => l != 0, (int)DiscRadius + Spacing);
        var old = shared.Discs.Where(d => block.At(d.Position) is int p && liftMask[p]).ToList();
        // The placed objects as the game will have them (PlacedNow without the discs replaced), near the block.
        var replaced = old.Select(d => d.Id).ToHashSet();
        var keep = shared.Placed.Concat(shared.Discs.Where(d => !replaced.Contains(d.Id)).Select(d => DiscAt(d.Position)))
            .Where(p => block.Near(p.Position, 100f)).ToList();

        // Where the ground needs a disc: near the lift or the discs replaced (not where ordinary edits
        // are), further from the ground without discs than the edits reach well.
        bool[] region = (bool[])liftMask.Clone();
        foreach (var (id, position) in old)
        {
            block.Disc(position, DiscReach, p => region[p] = true);
        }
        var (final, ground) = new BlockFit(block, terrain, targets, keep, region).Run();
        var (zones, worst) = BuildZoneEdits(world, terrain, block, targets, region, final, ground);

        // Natural objects where the lift moved the ground far.
        var clear = shared.Natural.Where(o => block.At(o.Position) is int p && MathF.Abs(targets.Lift[p]) > ClearHeight).Select(o => o.Id).ToList();
        var add = final.Select(d => new NewObject(nextId(), WorldSave.LocationProxyPrefab, d, Vector3.Zero, 0f, Fresh: false, Raw: DiscBytes(d))).ToList();
        return new Plan(zones, [.. old.Select(d => d.Id), .. clear], add, final.Count, old.Count, clear.Count, worst) { Ungenerated = targets.Ungenerated.Count };
    }

    /// <summary>Reads the ground asked for and the lift at each point of the block, and which points belong to generated zones.</summary>
    /// <param name="world">The world (which zones are generated).</param>
    /// <param name="block">The block worked on.</param>
    /// <param name="terrain">The terrain service (the base ground and masks).</param>
    /// <param name="edits">The edits so far.</param>
    /// <param name="lifted">The lifted zones.</param>
    private static BlockTargets ReadBlockTargets(WorldSave world, Block block, ValheimGen.TerrainService terrain, EditStore edits, List<ZoneEdit> lifted)
    {
        // The ground asked for, as it shows in the editor now (old discs, edits, lift), and the lift.
        float[] want = new float[block.N], lift = new float[block.N];
        var zones = new Dictionary<(int, int), ZoneEdit>();
        foreach (var (zx, zz) in block.Zones)
        {
            float[] b = terrain.BaseZone(zx, zz);
            ZoneEdit e = edits.Get(zx, zz) ?? new ZoneEdit(zx, zz);
            zones[(zx, zz)] = e;
            block.Each(zx, zz, (i, p) =>
            {
                want[p] = e.HeightAt(i, b[i]);
                lift[p] = e.Lift[i];
            });
        }

        // Lift in zones not generated yet is dropped (the game would generate them on the old ground).
        var ungenerated = new HashSet<(int, int)>();
        foreach (var e in lifted.Where(e => !Generated(world, e.ZoneX, e.ZoneZ)))
        {
            ungenerated.Add((e.ZoneX, e.ZoneZ));
        }
        bool[] open = new bool[block.N];
        foreach (var (zx, zz) in zones.Keys.Where(k => Generated(world, k.Item1, k.Item2)))
        {
            block.Each(zx, zz, (_, p) => open[p] = true);
        }
        foreach (var (zx, zz) in ungenerated)
        {
            // Points on the edge with a generated zone keep that zone's lift.
            block.Each(zx, zz, (_, p) =>
            {
                if (!open[p])
                {
                    want[p] -= lift[p];
                    lift[p] = 0;
                }
            });
        }
        return new BlockTargets(want, lift, zones, open, ungenerated);
    }

    /// <summary>Applies a plan to the edits (the zones, the objects taken away and added).</summary>
    public static void Apply(Plan plan, EditStore edits)
    {
        foreach (ZoneEdit z in plan.Zones)
        {
            edits.Put(z);
        }
        edits.SetDeleted(plan.Delete, true);
        edits.AddObjects(plan.Add);
    }
}
