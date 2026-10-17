using System.Numerics;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
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
}
