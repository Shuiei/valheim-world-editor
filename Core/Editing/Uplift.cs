using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Editing;

/// <summary>
/// No limit: ground beyond the game's ±8 m. The game clamps its saved ground edits (TerrainComp) to
/// the original ground ± 8 m, but not the flattening of locations: every player's game (console too)
/// levels a location's ground before it takes the original ground. So the editor places invisible
/// locations of the game's own (DevGround1: no model, it only levels a disc of 8 m radius to its
/// height and blends 5 m around it) as location proxies, and the game counts their ground as
/// generated. A shape sculpted past the limit (ZoneEdit.Lift) becomes, when saving or applying:
/// discs on a grid whose heights are fitted by working out the ground exactly as the game does
/// (TerrainModifiers), the rest as ordinary ground edits (within ±8 m of the new ground), the discs'
/// dirt painted over with the biome's own ground, and the trees, rocks and bushes where the ground
/// moved far taken away. The discs are worked out again wherever the ground is lifted again.
/// </summary>
public static class Uplift
{
    /// <summary>The game location used as a ground disc.</summary>
    public const string DiscName = "DevGround1";

    /// <summary>The disc location's name hash.</summary>
    public static readonly int Disc = StableHash.Of(DiscName);

    /// <summary>Whether a location (name hash) is a ground disc.</summary>
    public static bool IsDisc(int location) => location == Disc;

    /// <summary>DevGround1's TerrainModifier (as the game has it, GameData): level radius, smoothing radius, paint radius (metres).</summary>
    public const float DiscRadius = 8f, DiscReach = 12.96f, PaintRadius = 9.19f;

    /// <summary>The modifier's place in the location (it decides the order the game applies discs in).</summary>
    private static readonly Vector3 DiscOffset = new(-0.0006f, 0f, 0.1006f);

    /// <summary>Grid of disc middles (m), within reach of the ground they make.</summary>
    public const int Spacing = 6;

    /// <summary>A point further than this (metres) from the ground without discs needs one (the edits make up the rest).</summary>
    private const float Need = 5f;

    /// <summary>How many times the disc heights are fitted against the ground the game would make, and the passes over the discs each time.</summary>
    private const int Rounds = 8, Sweeps = 12;
    /// <summary>How much a disc resists moving (for discs with few points).</summary>
    private const double Damping = 0.25;

    /// <summary>Each fitting step moves a disc this share of the way (the game's order shifts as discs move).</summary>
    private const double Step = 0.8;

    /// <summary>Natural objects further than this (metres) from the ground they stood on are taken away.</summary>
    public const float ClearHeight = 2f;

    /// <summary>The object data keys of a location proxy's location and seed.</summary>
    private static readonly int LocationKey = StableHash.Of("location"), SeedKey = StableHash.Of("seed");

    /// <summary>What a save or an apply does to the edits.</summary>
    /// <param name="Zones">The zones' new ground.</param>
    /// <param name="Delete">Objects to take away (old discs, natural objects), by id.</param>
    /// <param name="Add">The new discs.</param>
    /// <param name="Discs">How many discs there are now.</param>
    /// <param name="OldDiscs">How many discs they replace.</param>
    /// <param name="Cleared">How many natural objects are taken away.</param>
    /// <param name="Worst">
    /// The largest difference left between the ground asked for and what the game will make (beyond the
    /// edits' ±8 m).
    /// </param>
    public sealed record Plan(List<ZoneEdit> Zones, List<int> Delete, List<NewObject> Add, int Discs, int OldDiscs, int Cleared, float Worst)
    {
        /// <summary>Lifted zones the game has not generated yet: their lift was dropped.</summary>
        public int Ungenerated { get; init; }

        /// <summary>The plan in a sentence for the user: discs, objects taken away, ground still off, zones left out.</summary>
        public string Describe() => $"{Discs} ground disc(s){(OldDiscs > 0 ? $" ({OldDiscs} replaced)" : "")}"
            + (Cleared > 0 ? $", {Cleared} tree(s)/rock(s) taken away" : "")
            + (Worst > EditStore.MaxLevel + 0.5f ? $"; some ground is up to {Worst - EditStore.MaxLevel:0.#} m off (too steep)" : "")
            + (Ungenerated > 0 ? $"; {Ungenerated} zone(s) not generated yet were left as they were (visit them in game first)" : "");
    }

    /// <summary>The editor's discs as they are now: the world's (not deleted) and the new ones not saved yet.</summary>
    public static List<(int Id, Vector3 Position)> CurrentDiscs(WorldSave world, EditStore edits)
    {
        var deleted = edits.Deleted;
        var list = world.Discs.Where(d => !deleted.Contains(d.Id)).ToList();
        list.AddRange(edits.Added.Where(IsDiscObject).Select(n => (n.Id, n.Position)));
        return list;
    }

    /// <summary>Whether a new object is a location proxy of the disc location.</summary>
    private static bool IsDiscObject(NewObject n) =>
        n.Prefab == WorldSave.LocationProxyPrefab && n.Raw != null && ZdoData.Parse(n.Raw).IntList.Any(i => i.Key == LocationKey && i.Value == Disc);

    /// <summary>
    /// The world's placed objects with modifiers as the game will have them: the world's discs replaced
    /// by the current ones (and, for a plan, without the ones it replaces).
    /// </summary>
    public static List<PlacedObject> PlacedNow(WorldSave world, EditStore edits, IEnumerable<(int Id, Vector3 Position)>? without = null)
    {
        var discs = CurrentDiscs(world, edits);
        if (without != null)
        {
            var drop = without.Select(d => d.Id).ToHashSet();
            discs = [.. discs.Where(d => !drop.Contains(d.Id))];
        }
        var placed = world.Placed.Where(p => !IsDisc(p.Location)).ToList();
        placed.AddRange(discs.Select(d => DiscAt(d.Position)));
        return placed;
    }

    /// <summary>A disc at a place, as a placed location for TerrainModifiers.</summary>
    private static PlacedObject DiscAt(Vector3 p) => new(WorldSave.LocationProxyPrefab, Disc, p, Vector3.Zero, 0);

    /// <summary>The disc as a saved object: a location proxy (persistent, solid, like the game's own).</summary>
    public static byte[] DiscBytes(Vector3 position)
    {
        var z = ZdoData.Parse(ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, 0x100 | (2 << 10), position, Vector3.Zero, 0f));
        z.Set("ints", LocationKey, Disc.ToString(System.Globalization.CultureInfo.InvariantCulture));
        z.Set("ints", SeedKey, "0");
        return z.Serialize();
    }

    /// <summary>A zone the save has room for (a chunk file: what WorldWriter needs); the running game takes any.</summary>
    public static bool Generated(WorldSave world, int zx, int zz) => world.IsLive || ChunkMath.Find(world.Chunks, zx, zz) != null;

    /// <summary>How many zones beyond a lifted one its discs and their ground can reach: the margin of its block.</summary>
    private static readonly int ReachZones = (int)MathF.Ceiling((Spacing + DiscRadius + DiscReach + 8f) / 64f);

    /// <summary>
    /// The plan for every lift waiting in the edits, or null when there is none. Each group of lifts near one another
    /// (<see cref="Clusters"/>) is planned alone, on only the zones near its lifts (<see cref="WorkingZones"/>), never
    /// on the box round them.
    /// </summary>
    /// <param name="world">The world, for its discs, objects and generated zones.</param>
    /// <param name="terrain">The generated ground the discs are fitted on.</param>
    /// <param name="edits">The edits with the lift; not changed (see <see cref="Apply"/>).</param>
    /// <param name="nextId">Gives each new disc object its (negative) id.</param>
    public static Plan? Make(WorldSave world, ValheimGen.TerrainService terrain, EditStore edits, Func<int> nextId)
    {
        var lifted = edits.Copies(e => e.HasLift);
        if (lifted.Count == 0)
        {
            return null;
        }
        var shared = Gather(world, edits);
        var plans = Clusters(lifted.Select(e => (e.ZoneX, e.ZoneZ)))
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
    private sealed record Shared(List<(int Id, Vector3 Position)> Discs, List<PlacedObject> Placed, List<(int Id, Vector3 Position)> Natural);

    /// <summary>Gathers <see cref="Shared"/> from the world and the edits.</summary>
    /// <param name="world">The world.</param>
    /// <param name="edits">The edits, for the discs added and the objects deleted.</param>
    private static Shared Gather(WorldSave world, EditStore edits)
    {
        var deleted = edits.Deleted;
        return new Shared(CurrentDiscs(world, edits), [.. world.Placed.Where(p => !IsDisc(p.Location))],
            [.. world.Objects.Where(o => !deleted.Contains(o.Id)
                && ObjectKinds.Of(PrefabCatalog.NameOf(o.Prefab), false) is ObjectKind.Trees or ObjectKind.Rocks or ObjectKind.Ore or ObjectKind.Bushes or ObjectKind.Pickables)
                .Select(o => (o.Id, o.Position))]);
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

    /// <summary>The plan for a group of lifted zones, worked on one block: them and the zones the discs reach from them.</summary>
    private static Plan MakeBlock(WorldSave world, ValheimGen.TerrainService terrain, EditStore edits, Func<int> nextId, List<ZoneEdit> lifted, Shared shared)
    {
        // The block of zones worked on: the lifted ones and those the discs reach from them.
        var block = new Block(WorkingZones(lifted.Select(e => (e.ZoneX, e.ZoneZ))));

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
                want[p] = b[i] + (e.Modified[i] ? Math.Clamp(e.Level[i] + e.Smooth[i], -EditStore.MaxLevel, EditStore.MaxLevel) : 0) + e.Lift[i];
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

        // Old discs near the lift are worked out again with the new ones.
        bool[] liftMask = Dilate(block, lift, l => l != 0, (int)DiscRadius + Spacing);
        var old = shared.Discs.Where(d => block.At(d.Position) is int p && liftMask[p]).ToList();
        // The placed objects as the game will have them (PlacedNow without the discs replaced), near the block.
        var replaced = old.Select(d => d.Id).ToHashSet();
        var keep = shared.Placed.Concat(shared.Discs.Where(d => !replaced.Contains(d.Id)).Select(d => DiscAt(d.Position)))
            .Where(p => block.Near(p.Position, 100f)).ToList();

        var raw = new Dictionary<(int, int), float[]>();
        float[] Ground(List<Vector3> discs)
        {
            var mods = new TerrainModifiers(keep.Concat(discs.Select(DiscAt)));
            float[] g = new float[block.N];
            foreach (var (zx, zz) in block.Zones)
            {
                if (!raw.TryGetValue((zx, zz), out float[]? r))
                {
                    r = raw[(zx, zz)] = terrain.RawZone(zx, zz);
                }
                float[] h = (float[])r.Clone();
                mods.Apply(zx, zz, h);
                block.Each(zx, zz, (i, p) => g[p] = h[i]);
            }
            return g;
        }

        // Where the ground needs a disc: near the lift or the discs replaced (not where ordinary edits
        // are), further from the ground without discs than the edits reach well.
        bool[] region = (bool[])liftMask.Clone();
        foreach (var (id, position) in old)
        {
            block.Disc(position, DiscReach, p => region[p] = true);
        }
        float[] bare = Ground([]);
        bool[] need = new bool[block.N];
        foreach (int p in block.Points)
        {
            need[p] = region[p] && open[p] && MathF.Abs(want[p] - bare[p]) > Need;
        }
        var nodes = new Dictionary<(int, int), float>();
        void AddNodesNear(int p)
        {
            var (wx, wz) = block.World(p);
            int r = (int)DiscRadius;
            for (int nz = (int)Math.Floor((wz - r) / (double)Spacing); nz <= (int)Math.Ceiling((wz + r) / (double)Spacing); nz++)
            {
                for (int nx = (int)Math.Floor((wx - r) / (double)Spacing); nx <= (int)Math.Ceiling((wx + r) / (double)Spacing); nx++)
                {
                    int cx = nx * Spacing, cz = nz * Spacing;
                    if ((cx - wx) * (cx - wx) + (cz - wz) * (cz - wz) <= DiscRadius * DiscRadius && !nodes.ContainsKey((cx, cz)) && block.At(new Vector3(cx, 0, cz)) is int q && open[q])
                    {
                        nodes[(cx, cz)] = want[q];
                    }
                }
            }
        }
        foreach (int p in block.Points)
        {
            if (need[p])
            {
                AddNodesNear(p);
            }
        }

        List<Vector3> Discs() => [.. nodes.Select(n => new Vector3(n.Key.Item1, n.Value, n.Key.Item2))];
        float[] ground = bare;
        // The game's order changes with the heights, which the fit does not see: each round's discs are
        // checked against the real ground, and the best kept.
        List<Vector3>? best = null;
        double bestMiss = double.MaxValue;
        double Miss(float[] g)
        {
            double m = 0;
            foreach (int p in block.Points)
            {
                if (region[p] || lift[p] != 0)
                {
                    // What the edits cannot make up counts most.
                    float r = MathF.Abs(want[p] - g[p]);
                    m += r * r + (r > EditStore.MaxLevel ? 100 * (r - EditStore.MaxLevel) * (r - EditStore.MaxLevel) : 0);
                }
            }
            return m;
        }
        for (int round = 0; round < Rounds && nodes.Count > 0; round++)
        {
            var discs = Discs();
            ground = Ground(discs);
            // The game's ground is a weighted mix of the disc heights (each disc levels its points to its
            // height, then blends the ring around it towards it), in the game's order: the disc heights
            // are fitted to it by least squares, a disc at a time.
            var columns = Weights(block, discs);
            // Each disc stays within the heights asked for around it: one mostly covered by others would
            // otherwise go far up or down for its few points, and its new height changes the game's order.
            var range = discs.Select(d =>
            {
                float lo = float.MaxValue, hi = float.MinValue;
                block.Disc(d, DiscRadius, p => { lo = MathF.Min(lo, want[p]); hi = MathF.Max(hi, want[p]); });
                return (lo, hi);
            }).ToList();
            float[] r = new float[block.N];
            foreach (int p in block.Points)
            {
                r[p] = want[p] - ground[p];
            }
            for (int sweep = 0; sweep < Sweeps; sweep++)
            {
                for (int k = 0; k < discs.Count; k++)
                {
                    double num = 0, den = Damping;
                    foreach (var (p, w) in columns[k])
                    {
                        num += w * r[p];
                        den += w * w;
                    }
                    float d = Math.Clamp((float)(Step * num / den), range[k].lo - discs[k].Y, range[k].hi - discs[k].Y);
                    foreach (var (p, w) in columns[k])
                    {
                        r[p] -= w * d;
                    }
                    discs[k] += new Vector3(0, d, 0);
                }
            }
            foreach (var d in discs)
            {
                nodes[((int)d.X, (int)d.Z)] = d.Y;
            }
            ground = Ground(Discs());
            double miss = Miss(ground);
            if (miss < bestMiss)
            {
                bestMiss = miss;
                best = Discs();
            }
            if (round < Rounds - 1)
            {
                // Still too far off out of the discs' reach (at a disc's blended edge): one more disc there.
                int before = nodes.Count;
                foreach (int p in block.Points)
                {
                    if (region[p] && open[p] && MathF.Abs(want[p] - ground[p]) > Need)
                    {
                        AddNodesNear(p);
                    }
                }
                if (nodes.Count == before && round >= 2)
                {
                    break;
                }
            }
        }
        var final = best ?? Discs();
        ground = final.Count > 0 ? Ground(final) : bare;

        // The zones' new edits: what the discs leave, within ±8 m; the discs' dirt painted back.
        float worst = 0;
        bool[] painted = new bool[block.N];
        foreach (var d in final)
        {
            block.Disc(d, PaintRadius + 1, p => painted[p] = true);
        }
        var result = new List<ZoneEdit>();
        foreach (var ((zx, zz), e) in zones)
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
                float r = want[p] - ground[p];
                if (lift[p] != 0 || region[p])
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

        // Natural objects where the lift moved the ground far.
        var clear = shared.Natural.Where(o => block.At(o.Position) is int p && MathF.Abs(lift[p]) > ClearHeight).Select(o => o.Id).ToList();
        var add = final.Select(d => new NewObject(nextId(), WorldSave.LocationProxyPrefab, d, Vector3.Zero, 0f, Fresh: false, Raw: DiscBytes(d))).ToList();
        return new Plan(result, [.. old.Select(d => d.Id), .. clear], add, final.Count, old.Count, clear.Count, worst) { Ungenerated = ungenerated.Count };
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

    /// <summary>
    /// For each disc, the points it reaches and how much of its height each ends up with: the game's
    /// Heightmap.LevelTerrain (the point takes the disc's height) then SmoothTerrain2 (the point moves a
    /// share 1 - (d/r)^3 towards it), discs in TerrainModifier.SortByModifiers order.
    /// </summary>
    private static List<(int P, float W)>[] Weights(Block block, List<Vector3> discs)
    {
        var at = new Dictionary<int, List<(int K, float W)>>();
        foreach (int k in Enumerable.Range(0, discs.Count).OrderBy(k => (discs[k] + DiscOffset).LengthSquared()))
        {
            block.Disc(discs[k], DiscReach, (p, d) =>
            {
                if (d <= DiscRadius)
                {
                    at[p] = [(k, 1f)];
                    return;
                }
                float u = d / DiscReach, t = 1f - u * u * u;
                if (!at.TryGetValue(p, out var list))
                {
                    at[p] = [(k, t)];
                    return;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    list[i] = (list[i].K, list[i].W * (1 - t));
                }
                list.Add((k, t));
            });
        }
        var columns = new List<(int, float)>[discs.Count];
        for (int k = 0; k < discs.Count; k++)
        {
            columns[k] = [];
        }
        foreach (var (p, list) in at)
        {
            foreach (var (k, w) in list)
            {
                if (w > 1e-4f)
                {
                    columns[k].Add((p, w));
                }
            }
        }
        return columns;
    }

    /// <summary>The points within r (a square, in points) of one whose value is on.</summary>
    private static bool[] Dilate(Block block, float[] values, Func<float, bool> on, int r)
    {
        bool[] src = new bool[block.N];
        for (int p = 0; p < block.N; p++)
        {
            src[p] = on(values[p]);
        }
        // Separable square dilation (rows, then columns): every point within r of one that is on.
        bool[] rows = new bool[block.N], res = new bool[block.N];
        block.Sweep(src, rows, r, false);
        block.Sweep(rows, res, r, true);
        return res;
    }

    /// <summary>
    /// Some zones as one grid of world points, one per metre (zones share their edge points). The points are kept in
    /// tiles of 64x64, one per zone of the block and per zone holding their far edges, so only the zones in the block
    /// take memory however far apart they are; a table of the tiles spans their box (one entry a zone).
    /// </summary>
    internal sealed class Block
    {
        /// <summary>Points a side of a tile: a zone's points without its far edges.</summary>
        private const int Tile = 64;

        /// <summary>Points in a tile.</summary>
        private const int TilePoints = Tile * Tile;

        /// <summary>The lowest zone x and z: the tile table's first tile.</summary>
        private readonly int _zx0, _zz0;

        /// <summary>The tile table's size in tiles (one more than the zones' box each way, for their far edges).</summary>
        private readonly int _tw, _th;

        /// <summary>Whether a tile's zone is in the block, by tile (rows of z).</summary>
        private readonly bool[] _member;

        /// <summary>Each tile's place in the point arrays (in tiles), or -1 when it has none, by tile (rows of z).</summary>
        private readonly int[] _slot;

        /// <summary>The tile (in the table) at each place in the point arrays.</summary>
        private readonly List<(int X, int Z)> _tiles = [];

        /// <summary>Whether a point belongs to a zone of the block (a far-edge tile also holds points of other zones).</summary>
        private readonly bool[] _inside;

        /// <summary>The world position of the first tile's first point (metres).</summary>
        public readonly int X0, Z0;

        /// <summary>The block's zones, by rows (z, then x).</summary>
        public IReadOnlyList<(int X, int Z)> Zones { get; }

        /// <summary>The size of the point arrays (points outside the block's zones included).</summary>
        public int N { get; }

        /// <summary>The points of the block's zones, by rows of the world (z, then x), each once.</summary>
        public int[] Points { get; }

        /// <summary>A block of the given zones.</summary>
        /// <param name="zones">The zones, by rows (z, then x), each once.</param>
        public Block(List<(int X, int Z)> zones)
        {
            Zones = zones;
            _zx0 = zones.Min(z => z.X);
            _zz0 = zones.Min(z => z.Z);
            _tw = zones.Max(z => z.X) - _zx0 + 2;
            _th = zones.Max(z => z.Z) - _zz0 + 2;
            X0 = _zx0 * 64 - 32;
            Z0 = _zz0 * 64 - 32;
            _member = new bool[_tw * _th];
            foreach (var (x, z) in zones)
            {
                _member[(z - _zz0) * _tw + x - _zx0] = true;
            }
            bool Member(int tx, int tz) => tx >= 0 && tz >= 0 && _member[tz * _tw + tx];
            _slot = new int[_tw * _th];
            for (int tz = 0; tz < _th; tz++)
            {
                for (int tx = 0; tx < _tw; tx++)
                {
                    bool used = Member(tx, tz) || Member(tx - 1, tz) || Member(tx, tz - 1) || Member(tx - 1, tz - 1);
                    _slot[tz * _tw + tx] = used ? _tiles.Count : -1;
                    if (used)
                    {
                        _tiles.Add((tx, tz));
                    }
                }
            }
            N = _tiles.Count * TilePoints;
            _inside = new bool[N];
            foreach (var (zx, zz) in zones)
            {
                Each(zx, zz, (_, p) => _inside[p] = true);
            }
            var points = new List<int>();
            for (int tz = 0; tz < _th; tz++)
            {
                for (int oz = 0; oz < Tile; oz++)
                {
                    for (int tx = 0; tx < _tw; tx++)
                    {
                        int s = _slot[tz * _tw + tx];
                        for (int ox = 0; s >= 0 && ox < Tile; ox++)
                        {
                            int p = s * TilePoints + oz * Tile + ox;
                            if (_inside[p])
                            {
                                points.Add(p);
                            }
                        }
                    }
                }
            }
            Points = [.. points];
        }

        /// <summary>The place of a point (in points from the first one) in the arrays, or -1 when no tile holds it.</summary>
        private int Slot(int x, int z)
        {
            if (x < 0 || z < 0 || x >= _tw * Tile || z >= _th * Tile)
            {
                return -1;
            }
            int s = _slot[z / Tile * _tw + x / Tile];
            return s < 0 ? -1 : s * TilePoints + z % Tile * Tile + x % Tile;
        }

        /// <summary>The place of a point (in points from the first one) in the arrays, or -1 when it is not in the block's zones.</summary>
        private int Index(int x, int z)
        {
            int p = Slot(x, z);
            return p >= 0 && _inside[p] ? p : -1;
        }

        /// <summary>A point's world position (metres).</summary>
        public (int X, int Z) World(int p)
        {
            var (tx, tz) = _tiles[p / TilePoints];
            int q = p % TilePoints;
            return (X0 + tx * Tile + q % Tile, Z0 + tz * Tile + q / Tile);
        }

        /// <summary>The point nearest a world position (halves rounded up, as a disc's middle), or null outside the block's
        /// zones.</summary>
        public int? At(Vector3 v)
        {
            int p = Index(Nearest(v.X) - X0, Nearest(v.Z) - Z0);
            return p >= 0 ? p : null;
        }

        /// <summary>Whether a world position is within margin metres of one of the block's zones (of its square of points).</summary>
        public bool Near(Vector3 v, float margin)
        {
            int zx0 = (int)MathF.Floor((v.X - 32 - margin) / 64) - _zx0, zx1 = (int)MathF.Ceiling((v.X + 32 + margin) / 64) - _zx0;
            int zz0 = (int)MathF.Floor((v.Z - 32 - margin) / 64) - _zz0, zz1 = (int)MathF.Ceiling((v.Z + 32 + margin) / 64) - _zz0;
            for (int tz = Math.Max(0, zz0); tz <= Math.Min(_th - 1, zz1); tz++)
            {
                for (int tx = Math.Max(0, zx0); tx <= Math.Min(_tw - 1, zx1); tx++)
                {
                    int x = X0 + tx * Tile, z = Z0 + tz * Tile;
                    if (_member[tz * _tw + tx] && v.X >= x - margin && v.Z >= z - margin && v.X <= x + Tile + margin && v.Z <= z + Tile + margin)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>A zone's 65x65 points: (index in the zone, point in the block).</summary>
        public void Each(int zx, int zz, Action<int, int> f)
        {
            int ox = (zx - _zx0) * Tile, oz = (zz - _zz0) * Tile;
            for (int k = 0; k < 65; k++)
            {
                for (int l = 0; l < 65; l++)
                {
                    f(k * 65 + l, Slot(ox + l, oz + k));
                }
            }
        }

        /// <summary>The whole metre nearest a world coordinate, halves rounded up (as Heightmap.WorldToVertex).</summary>
        private static int Nearest(float v) => (int)MathF.Floor(v + 0.5f);

        /// <summary>The points within r of a disc's middle (as Heightmap.LevelTerrain counts them).</summary>
        public void Disc(Vector3 c, float r, Action<int> f) => Disc(c, r, (p, _) => f(p));

        /// <summary>The points within r of a disc's middle, with their distance from it (metres).</summary>
        public void Disc(Vector3 c, float r, Action<int, float> f)
        {
            int cx = Nearest(c.X) - X0, cz = Nearest(c.Z) - Z0, n = (int)MathF.Ceiling(r);
            for (int z = cz - n; z <= cz + n; z++)
            {
                for (int x = cx - n; x <= cx + n; x++)
                {
                    int p = Index(x, z);
                    if (p < 0)
                    {
                        continue;
                    }
                    float dx = x - cx, dz = z - cz;
                    float d = MathF.Sqrt(dx * dx + dz * dz);
                    if (d <= r)
                    {
                        f(p, d);
                    }
                }
            }
        }

        /// <summary>
        /// One pass of a square dilation: each point is on in <paramref name="to"/> when a point within r of it on its
        /// row (or column) is on in <paramref name="from"/>. Tiles missing on the way count as off.
        /// </summary>
        /// <param name="from">The points that are on.</param>
        /// <param name="to">Gets the points within r of them.</param>
        /// <param name="r">How far (points).</param>
        /// <param name="columns">Along the columns (z) rather than the rows (x).</param>
        public void Sweep(bool[] from, bool[] to, int r, bool columns)
        {
            int lines = columns ? _tw : _th, along = columns ? _th : _tw;
            for (int a = 0; a < lines; a++)
            {
                for (int o = 0; o < Tile; o++)
                {
                    int last = int.MinValue / 2;
                    for (int b = 0; b < along; b++)
                    {
                        int s = columns ? _slot[b * _tw + a] : _slot[a * _tw + b];
                        for (int i = 0; s >= 0 && i < Tile; i++)
                        {
                            int p = s * TilePoints + (columns ? i * Tile + o : o * Tile + i), at = b * Tile + i;
                            if (from[p])
                            {
                                last = at;
                            }
                            to[p] = at - last <= r;
                        }
                    }
                    last = int.MaxValue / 2;
                    for (int b = along - 1; b >= 0; b--)
                    {
                        int s = columns ? _slot[b * _tw + a] : _slot[a * _tw + b];
                        for (int i = Tile - 1; s >= 0 && i >= 0; i--)
                        {
                            int p = s * TilePoints + (columns ? i * Tile + o : o * Tile + i), at = b * Tile + i;
                            if (from[p])
                            {
                                last = at;
                            }
                            to[p] |= last - at <= r;
                        }
                    }
                }
            }
        }
    }
}
