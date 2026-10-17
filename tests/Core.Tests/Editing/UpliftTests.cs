using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;
using TerrainService = ValheimGen.TerrainService;

namespace WorldEditor.Tests;

/// <summary>
/// No limit ground (Uplift): ground lifted past the game's ±8 m becomes invisible ground discs (location proxies the
/// game levels the ground for) plus ordinary edits. After saving, the ground the game makes from the save (generated
/// ground, the discs, the edits clamped to ±8 m) must be the ground asked for.
/// </summary>
[Collection("World files")]
public class UpliftTests
{
    /// <summary>
    /// A world with its terrain (with modifiers) and edits: the ground as the editor shows it, lifted, and saved.
    /// </summary>
    private sealed class Open
    {
        public required WorldSave World { get; init; }
        public required TerrainService Terrain { get; init; }
        public required EditStore Edits { get; init; }
        private int _next = -1;
        public int NextId() => _next--;

        public static Open Of(WorldSave world)
        {
            var mods = new TerrainModifiers(world);
            return new Open { World = world, Terrain = new TerrainService(world, mods), Edits = new EditStore(world) };
        }

        // The ground as the editor shows it (edits clamped to ±8 m, plus any lift not saved yet).
        public float Height(float x, float z)
        {
            int zx = (int)MathF.Floor((x + 32) / 64), zz = (int)MathF.Floor((z + 32) / 64);
            int i = ((int)MathF.Round(z) - (zz * 64 - 32)) * 65 + (int)MathF.Round(x) - (zx * 64 - 32);
            float b = Terrain.BaseZone(zx, zz)[i];
            var e = Edits.Get(zx, zz);
            return b + (e != null && e.Modified[i] ? Math.Clamp(e.Level[i] + e.Smooth[i], -8, 8) : 0) + (e?.Lift[i] ?? 0);
        }

        // Lifts the ground by f(x, z) (No limit) over the zones around (cx, cz).
        public void Lift(float cx, float cz, Func<float, float, float> f)
        {
            int zc = (int)MathF.Floor((cx + 32) / 64), zr = (int)MathF.Floor((cz + 32) / 64);
            for (int zz = zr - 2; zz <= zr + 2; zz++)
            {
                for (int zx = zc - 2; zx <= zc + 2; zx++)
                {
                    var e = Edits.Get(zx, zz) ?? new ZoneEdit(zx, zz);
                    for (int i = 0; i < EditStore.Cells; i++)
                    {
                        e.Lift[i] += f(zx * 64 - 32 + i % 65, zz * 64 - 32 + i / 65);
                    }
                    Edits.Put(e);
                }
            }
        }

        public Uplift.Plan Save()
        {
            var plan = Uplift.Make(World, Terrain, Edits, NextId)!;
            Uplift.Apply(plan, Edits);
            var r = WorldWriter.Save(World, [.. Edits.All().Where(e => e.Changed)], Edits.Deleted, Edits.Added, Edits.Resets);
            Assert.True(r.Saved, r.Message);
            Assert.True(r.Skipped.Count == 0, string.Join("; ", r.Skipped.Take(3)));
            return plan;
        }
    }

    /// <summary>
    /// A smooth hill (or hollow) of the given height and radius.
    /// </summary>
    private static Func<float, float, float> Bell(float cx, float cz, float height, float radius) => (x, z) =>
    {
        float d = MathF.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / radius;
        return d >= 1 ? 0 : height * (1 - d * d) * (1 - d * d);
    };

    /// <summary>
    /// The ground asked for and the ground the saved world makes, over a square around (cx, cz).
    /// </summary>
    private static List<float> Misses(Func<float, float, float> want, Open after, float cx, float cz, int half)
    {
        var misses = new List<float>();
        for (int z = (int)cz - half; z <= (int)cz + half; z += 2)
        {
            for (int x = (int)cx - half; x <= (int)cx + half; x += 2)
            {
                misses.Add(MathF.Abs(want(x, z) - after.Height(x, z)));
            }
        }
        misses.Sort();
        return misses;
    }

    [Fact]
    public void AHillPastTheLimitIsSavedAsGroundDiscsAndComesOutAsAsked()
    {
        using var w = new TempWorld();
        var o = Open.Of(w.Load());
        // Within the test world's saved zones (0 to 15 both ways), over the edge of its trees.
        const float Cx = 40, Cz = 30;
        var hill = Bell(Cx, Cz, 30, 60);
        var (id, prefab, position, rotation, scale) = o.World.Objects.Where(t => ObjectKinds.Of(PrefabCatalog.NameOf(t.Prefab), false) == ObjectKind.Trees)
            .OrderBy(t => Vector2.Distance(new(t.Position.X, t.Position.Z), new(Cx, Cz))).First();
        Assert.True(hill(position.X, position.Z) > Uplift.ClearHeight);
        var before = new Dictionary<(int, int), float>();
        for (int z = (int)Cz - 70; z <= (int)Cz + 70; z += 2)
        {
            for (int x = (int)Cx - 70; x <= (int)Cx + 70; x += 2)
            {
                before[(x, z)] = o.Height(x, z);
            }
        }
        int piecesBefore = o.World.Pieces.Count;
        o.Lift(Cx, Cz, hill);
        var plan = o.Save();
        Assert.True(plan.Discs > 20, plan.Describe().English);
        Assert.Equal(0, plan.OldDiscs);

        var after = Open.Of(w.Load());
        Assert.Equal(plan.Discs, after.World.Discs.Count);
        // No lift is left to save: it is all ground discs and edits now.
        Assert.DoesNotContain(after.Edits.All(), e => e.HasLift);
        var misses = Misses((x, z) => before[((int)x, (int)z)] + hill(x, z), after, Cx, Cz, 70);
        Assert.True(misses[misses.Count * 99 / 100] < 0.5f, $"99% within 0.5 m: {misses[misses.Count * 99 / 100]:0.00}");
        Assert.True(misses[^1] < 4f, $"worst {misses[^1]:0.00} m");
        // The top really is beyond the edits' reach: the ground discs carry it.
        Assert.True(after.Height(Cx, Cz) - before[((int)Cx, (int)Cz)] > 25);
        // The tree under the hill is taken away; building pieces are not.
        Assert.DoesNotContain(after.World.Objects, t => t.Prefab == prefab && Vector3.Distance(t.Position, position) < 0.01f);
        Assert.Equal(piecesBefore, after.World.Pieces.Count);
        // Invisible: not an object of the View, and not outlined as a location's flattening.
        Assert.DoesNotContain(after.World.Objects, t => t.Prefab == WorldSave.LocationProxyPrefab);
        Assert.All(new TerrainModifiers(after.World).InZone((int)MathF.Floor((Cx + 32) / 64), (int)MathF.Floor((Cz + 32) / 64)).Where(m => m.LevelRadius == Uplift.DiscRadius), m => Assert.True(m.Disc));
    }

    [Fact]
    public void LiftingAgainReplacesTheDiscsAndLoweringItAllBackTakesThemAway()
    {
        using var w = new TempWorld();
        var o = Open.Of(w.Load());
        const float Cx = 300, Cz = 300;
        float start = o.Height(Cx, Cz);
        o.Lift(Cx, Cz, Bell(Cx, Cz, 25, 50));
        o.Save();

        // Higher: the old discs are worked out again with new ones.
        var again = Open.Of(w.Load());
        int discs = again.World.Discs.Count;
        again.Lift(Cx, Cz, Bell(Cx, Cz, 15, 50));
        var plan = again.Save();
        Assert.True(plan.OldDiscs > 0);
        var third = Open.Of(w.Load());
        Assert.InRange(third.Height(Cx, Cz) - start, 38, 42);
        Assert.True(third.World.Discs.Count >= discs / 2);

        // All the way back down: no ground disc is needed any more.
        third.Lift(Cx, Cz, Bell(Cx, Cz, -40, 50));
        third.Save();
        var back = Open.Of(w.Load());
        Assert.Empty(back.World.Discs);
        Assert.InRange(back.Height(Cx, Cz) - start, -1, 1);
    }

    [Fact]
    public void TheGroundCanGoDownPastTheLimitToo()
    {
        using var w = new TempWorld();
        var o = Open.Of(w.Load());
        const float Cx = 400, Cz = 200;
        float start = o.Height(Cx, Cz);
        o.Lift(Cx, Cz, Bell(Cx, Cz, -25, 50));
        var plan = o.Save();
        Assert.True(plan.Discs > 0);
        var after = Open.Of(w.Load());
        Assert.InRange(after.Height(Cx, Cz) - start, -26, -24);
    }

    [Fact]
    public void WithoutLiftThereIsNothingToDo()
    {
        using var w = new TempWorld();
        var o = Open.Of(w.Load());
        Assert.Null(Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId));
    }

    [Fact]
    public void ADiscIsALocationProxyTheGameSpawnsAsDevGround1()
    {
        var z = ZdoData.Parse(Uplift.DiscBytes(new Vector3(10, 55, -20)));
        Assert.Equal(WorldSave.LocationProxyPrefab, z.Prefab);
        Assert.Equal(55, z.Position.Y, 0.001f);
        Assert.Contains(z.IntList, i => i.Key == StableHash.Of("location") && i.Value == StableHash.Of("DevGround1"));
        Assert.Contains(z.IntList, i => i.Key == StableHash.Of("seed"));
        // Persistent (kept in the save), a solid object like the game's own proxies.
        Assert.Equal(0x100 | (2 << 10), z.BaseFlags & 0xFF00 & ~0x1000 & ~0x2000);
    }

    [Fact]
    public void LiftInZonesTheGameHasNotGeneratedIsDropped()
    {
        using var w = new TempWorld();
        var o = Open.Of(w.Load());
        // On the edge of the saved zones: zone 15 is generated, 16 is not.
        const float Cx = 15 * 64 + 32, Cz = 300;
        o.Lift(Cx, Cz, Bell(Cx, Cz, 20, 40));
        var plan = Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId)!;
        Assert.True(plan.Ungenerated > 0, plan.Describe().English);
        Assert.Contains(plan.Zones, e => e.ZoneX >= 16);
        Assert.All(plan.Zones.Where(e => e.ZoneX >= 16), e => Assert.False(e.HasLift));
        Assert.Contains("not generated yet", plan.Describe().English);
    }

    [Fact]
    public void LiftsFarApartAreGroupedApart()
    {
        // The working block was the box round every lifted zone: lifts at two ends of the world made one grid of
        // billions of points. Zones whose working zones (a zone round each) overlap or touch go together, the others apart.
        var groups = Uplift.Clusters([(0, 0), (1, 0), (100, 100), (4, 0), (8, 0), (1, 0)]);
        Assert.Equal(3, groups.Count);
        Assert.Equal([(0, 0), (1, 0), (4, 0)], groups[0]);
        Assert.Equal([(100, 100)], groups[1]);
        Assert.Equal([(8, 0)], groups[2]);
        // A chain of near ones is one group.
        Assert.Single(Uplift.Clusters([(0, 0), (3, 3), (6, 6), (9, 9)]));
        // A zone far from every other is a group of its own, even inside the box round another group.
        var ring = Uplift.Clusters([(0, 0), (3, 0), (6, 0), (9, 0), (12, 0), (12, 3), (12, 6), (12, 9), (12, 12), (0, 12)]);
        Assert.Equal(2, ring.Count);
        Assert.Equal([(0, 12)], ring[1]);
        // Whatever the zones, no two groups' working zones overlap or touch: 60 zones scattered by fixed steps over
        // 30 × 31, which make groups of one to twenty.
        var many = Uplift.Clusters([.. Enumerable.Range(0, 60).Select(i => (i * 37 % 30 - 15, (i * 53 + 29) % 31 - 15))]);
        Assert.True(many.Count > 1);
        for (int i = 0; i < many.Count; i++)
        {
            for (int j = i + 1; j < many.Count; j++)
            {
                foreach (var (a, b) in many[i].SelectMany(a => many[j].Select(b => (a, b))))
                {
                    Assert.True(Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z)) > 3, $"zones {a} and {b} meet");
                }
            }
        }
    }

    [Fact]
    public void ASparseChainOfLiftsIsWorkedOnlyNearItsLifts()
    {
        // A chain of lifts each three zones from the next is one group, but its working zones are only those round
        // each lift (a zone each way), not the box round the chain (300 zones a side).
        var chain = Enumerable.Range(0, 100).Select(i => (X: 3 * i, Z: 3 * i)).ToList();
        Assert.Single(Uplift.Clusters(chain));
        var zones = Uplift.WorkingZones(chain);
        Assert.Equal(100 * 9, zones.Count);
        Assert.All(zones, z => Assert.Contains(chain, c => Math.Abs(c.X - z.X) <= 1 && Math.Abs(c.Z - z.Z) <= 1));
        // Lifts side by side fill their box: the zones worked on are the box and a zone round it, as before.
        var square = Uplift.WorkingZones([(0, 0), (1, 0), (0, 1), (1, 1), (2, 1), (2, 0)]);
        Assert.Equal([.. from z in Enumerable.Range(-1, 4) from x in Enumerable.Range(-1, 5) select (x, z)], square);
    }

    [Fact]
    public void AChainOfHillsIsPlannedOnTheZonesNearThem()
    {
        // Three hills along a diagonal of the test world, three zones apart: one group, worked only near the hills.
        using var w = new TempWorld();
        var o = Open.Of(w.Load());
        var hills = new[] { (X: 2 * 64f, Z: 2 * 64f), (X: 5 * 64f, Z: 5 * 64f), (X: 8 * 64f, Z: 8 * 64f) };
        foreach (var (x, z) in hills)
        {
            o.Lift(x, z, Bell(x, z, 20, 40));
        }
        var lifted = o.Edits.All().Where(e => e.HasLift).Select(e => (e.ZoneX, e.ZoneZ)).ToList();
        Assert.Single(Uplift.Clusters(lifted));
        var near = Uplift.WorkingZones(lifted).ToHashSet();
        var plan = Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId)!;
        Assert.All(plan.Zones, e => Assert.Contains((e.ZoneX, e.ZoneZ), near));
        // Every hill gets its discs.
        Assert.All(hills, h => Assert.Contains(plan.Add, n => Vector2.Distance(new(n.Position.X, n.Position.Z), new(h.X, h.Z)) < 40));
    }

    [Fact]
    public void TwoLiftsFarApartArePlannedAsEachAlone()
    {
        using var w = new TempWorld();
        var (a, b) = ((X: 40f, Z: 30f), (X: 13 * 64f, Z: 13 * 64f));
        var both = Open.Of(w.Load());
        both.Lift(a.X, a.Z, Bell(a.X, a.Z, 20, 40));
        both.Lift(b.X, b.Z, Bell(b.X, b.Z, 20, 40));
        var plan = Uplift.Make(both.World, both.Terrain, both.Edits, both.NextId)!;
        var alone = new List<Uplift.Plan>();
        foreach (var (x, z) in new[] { a, b })
        {
            var o = Open.Of(w.Load());
            o.Lift(x, z, Bell(x, z, 20, 40));
            alone.Add(Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId)!);
        }
        Assert.All(alone, p => Assert.True(p.Discs > 0, p.Describe().English));
        Assert.Equal(alone.Sum(p => p.Discs), plan.Discs);
        Assert.Equal(alone.SelectMany(p => p.Add).Select(n => n.Position).Order(Comparer<Vector3>.Create((u, v) => (u.X, u.Z).CompareTo((v.X, v.Z)))),
            plan.Add.Select(n => n.Position).Order(Comparer<Vector3>.Create((u, v) => (u.X, u.Z).CompareTo((v.X, v.Z)))));
        Assert.Equal(alone.Sum(p => p.Zones.Count), plan.Zones.Count);
        Assert.Equal(plan.Zones.Count, plan.Zones.Select(e => (e.ZoneX, e.ZoneZ)).Distinct().Count());
        Assert.Equal(alone.Max(p => p.Worst), plan.Worst);
    }

    [Theory]
    [InlineData(0.5f, 0.5f)]
    [InlineData(-1.5f, 2.5f)]
    [InlineData(3.2f, -0.7f)]
    public void AnObjectAndADiscCentredOnItTakeTheSamePoint(float x, float z)
    {
        // At rounded halves to even (MathF.Round) and Disc rounded them up: an object at x.5 was taken for another
        // point than a disc centred on it.
        var block = new Uplift.Block([.. from zz in Enumerable.Range(-1, 3) from zx in Enumerable.Range(-1, 3) select (zx, zz)]);
        var at = new Vector3(x, 0, z);
        var middle = new List<int>();

        block.Disc(at, 0, middle.Add);

        Assert.Equal(block.At(at), Assert.Single(middle));
    }
}
