using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    /// <summary>
    /// The plan for every lift waiting in the edits, or null when there is none. Each group of lifts near one another
    /// (<see cref="Clusters"/>) is planned alone, on only the zones near its lifts (<see cref="WorkingZones"/>), never
    /// on the box round them; a group whose lifted zones are all as their discs were worked out
    /// (<paramref name="asPlanned"/>) is not planned again.
    /// </summary>
    /// <param name="world">The world, for its discs, objects and generated zones.</param>
    /// <param name="terrain">The generated ground the discs are fitted on.</param>
    /// <param name="edits">The edits with the lift; not changed (see <see cref="Apply"/>).</param>
    /// <param name="nextId">Gives each new disc object its (negative) id.</param>
    /// <param name="standing">
    /// A zone's edits as the ground the world's objects stand on now has them (live: as last sent to the game); null:
    /// as the world's files hold them.
    /// </param>
    /// <param name="asPlanned">
    /// Whether a lifted zone is still as the discs the world holds were worked out from (EditStore.AsPlanned): applied
    /// live, or in the save the world was read from; null: every group is planned.
    /// </param>
    public static Plan? Make(WorldSave world, ValheimGen.TerrainService terrain, EditStore edits, Func<int> nextId, Func<int, int, ZoneEdit?>? standing = null,
        Func<int, int, bool>? asPlanned = null)
    {
        var lifted = edits.Copies(e => e.HasLift);
        var groups = Clusters(lifted.Select(e => (e.ZoneX, e.ZoneZ))).Where(c => asPlanned == null || !c.All(z => asPlanned(z.X, z.Z))).ToList();
        if (groups.Count == 0)
        {
            return null;
        }
        var shared = Gather(world, edits, standing);
        var plans = groups
            .Select(c => c.ToHashSet())
            .Select(c => MakeBlock(world, terrain, edits, nextId, [.. lifted.Where(e => c.Contains((e.ZoneX, e.ZoneZ)))], shared))
            .ToList();
        return plans.Count == 1 ? plans[0] : new Plan([.. plans.SelectMany(p => p.Zones)], [.. plans.SelectMany(p => p.Delete)], [.. plans.SelectMany(p => p.Add)],
            plans.Sum(p => p.Discs), plans.Sum(p => p.OldDiscs), plans.Sum(p => p.Cleared), plans.Max(p => p.Worst))
        { Ungenerated = plans.Sum(p => p.Ungenerated) };
    }

    /// <summary>
    /// What every group's plan looks through, gathered once for all of them: the discs as they are now
    /// (<see cref="CurrentDiscs"/>), the world's placed objects that are not discs, and its natural objects not deleted
    /// (trees, rocks, ore, bushes, pickables), in the world's order.
    /// </summary>
    /// <param name="Discs">The discs as they are now.</param>
    /// <param name="Placed">The placed objects with modifiers that are not discs.</param>
    /// <param name="Natural">The natural objects a lift may take away.</param>
    /// <param name="Standing">A zone's edits as the ground the natural objects stand on now has them.</param>
    private sealed record Shared(List<(int Id, Vector3 Position)> Discs, List<PlacedObject> Placed, List<(int Id, Vector3 Position)> Natural, Func<int, int, ZoneEdit?> Standing);

    /// <summary>Gathers <see cref="Shared"/> from the world and the edits.</summary>
    /// <param name="world">The world.</param>
    /// <param name="edits">The edits, for the discs added and the objects deleted.</param>
    /// <param name="standing">See <see cref="Make"/>; null: the world's files.</param>
    private static Shared Gather(WorldSave world, EditStore edits, Func<int, int, ZoneEdit?>? standing)
    {
        var deleted = edits.Deleted;
        if (standing == null)
        {
            // The world's zones by place (the first whole one of two in one zone, as the edit store takes it), each made
            // an edit only when a block asks for it: a block needs a few, the world may hold thousands.
            var saved = new Dictionary<(int, int), TerrainZone>();
            foreach (TerrainZone z in world.TerrainZones.Where(z => z.ModifiedHeight.Length == EditStore.Cells && z.ModifiedPaint.Length == EditStore.Cells))
            {
                saved.TryAdd((z.ZoneX, z.ZoneZ), z);
            }
            var made = new Dictionary<(int, int), ZoneEdit?>();
            standing = (zx, zz) => made.TryGetValue((zx, zz), out var e) ? e
                : made[(zx, zz)] = saved.TryGetValue((zx, zz), out var z) ? EditStore.Of(z) : null;
        }
        return new Shared(CurrentDiscs(world, edits), [.. world.Placed.Where(p => !IsDisc(p.Location))],
            [.. world.Objects.Where(o => !deleted.Contains(o.Id)
                && ObjectKinds.Of(PrefabCatalog.NameOf(o.Prefab), false) is ObjectKind.Trees or ObjectKind.Rocks or ObjectKind.Ore or ObjectKind.Bushes or ObjectKind.Pickables)
                .Select(o => (o.Id, o.Position))], standing);
    }

    /// <summary>
    /// Lifted zones in connected groups, in the order first met (and in that order within a group): two zones whose
    /// working zones (<see cref="WorkingZones"/>) overlap or touch are in one group. A group's discs and ground never
    /// reach another's zones, and no zone is worked on by two groups, so each group is planned alone.
    /// </summary>
    /// <param name="zones">The lifted zones.</param>
    internal static List<List<(int X, int Z)>> Clusters(IEnumerable<(int X, int Z)> zones)
    {
        var order = zones.Distinct().ToList();
        var index = new Dictionary<(int X, int Z), int>();
        for (int i = 0; i < order.Count; i++)
        {
            index[order[i]] = i;
        }
        // Two zones whose working zones overlap or touch: at most this far apart either way.
        int gap = 2 * ReachZones + 1;
        bool[] met = new bool[order.Count];
        var groups = new List<List<(int X, int Z)>>();
        for (int i = 0; i < order.Count; i++)
        {
            if (met[i])
            {
                continue;
            }
            met[i] = true;
            var members = new List<int> { i };
            for (int k = 0; k < members.Count; k++)
            {
                var (x, z) = order[members[k]];
                for (int dz = -gap; dz <= gap; dz++)
                {
                    for (int dx = -gap; dx <= gap; dx++)
                    {
                        if (index.TryGetValue((x + dx, z + dz), out int j) && !met[j])
                        {
                            met[j] = true;
                            members.Add(j);
                        }
                    }
                }
            }
            members.Sort();
            groups.Add([.. members.Select(m => order[m])]);
        }
        return groups;
    }

    /// <summary>
    /// The zones a group of lifted zones is worked on: each lifted zone and those within <see cref="ReachZones"/> of
    /// it, by rows (z, then x). Only these: a sparse chain of lifts takes the zones near its lifts, not the box round
    /// the chain; a group that fills its box (lifts side by side) takes the whole box, as before.
    /// </summary>
    /// <param name="lifted">The group's lifted zones.</param>
    internal static List<(int X, int Z)> WorkingZones(IEnumerable<(int X, int Z)> lifted)
    {
        var zones = new HashSet<(int X, int Z)>();
        foreach (var (x, z) in lifted)
        {
            for (int dz = -ReachZones; dz <= ReachZones; dz++)
            {
                for (int dx = -ReachZones; dx <= ReachZones; dx++)
                {
                    zones.Add((x + dx, z + dz));
                }
            }
        }
        return [.. zones.OrderBy(t => t.Z).ThenBy(t => t.X)];
    }
}
