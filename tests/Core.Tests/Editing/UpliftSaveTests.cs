using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;
using static WorldEditor.Tests.UpliftWorld;

namespace WorldEditor.Tests;

/// <summary>
/// No limit ground (Uplift) in the saved world: ground lifted past the game's ±8 m becomes invisible ground discs (location
/// proxies the game levels the ground for) plus ordinary edits. After saving, the ground the game makes from the save
/// (generated ground, the discs, the edits clamped to ±8 m) must be the ground asked for.
/// </summary>
[Collection("World files")]
public class UpliftSaveTests
{
    /// <summary>
    /// The ground asked for and the ground the saved world makes, over a square around (cx, cz).
    /// </summary>
    private static List<float> Misses(Func<float, float, float> want, UpliftWorld after, float cx, float cz, int half)
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
        var o = UpliftWorld.Of(w.Load());
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

        var after = UpliftWorld.Of(w.Load());
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
        var o = UpliftWorld.Of(w.Load());
        const float Cx = 300, Cz = 300;
        float start = o.Height(Cx, Cz);
        o.Lift(Cx, Cz, Bell(Cx, Cz, 25, 50));
        o.Save();

        // Higher: the old discs are worked out again with new ones.
        var again = UpliftWorld.Of(w.Load());
        int discs = again.World.Discs.Count;
        again.Lift(Cx, Cz, Bell(Cx, Cz, 15, 50));
        var plan = again.Save();
        Assert.True(plan.OldDiscs > 0);
        var third = UpliftWorld.Of(w.Load());
        Assert.InRange(third.Height(Cx, Cz) - start, 38, 42);
        Assert.True(third.World.Discs.Count >= discs / 2);

        // All the way back down: no ground disc is needed any more.
        third.Lift(Cx, Cz, Bell(Cx, Cz, -40, 50));
        third.Save();
        var back = UpliftWorld.Of(w.Load());
        Assert.Empty(back.World.Discs);
        Assert.InRange(back.Height(Cx, Cz) - start, -1, 1);
    }

    [Fact]
    public void TheGroundCanGoDownPastTheLimitToo()
    {
        using var w = new TempWorld();
        var o = UpliftWorld.Of(w.Load());
        const float Cx = 400, Cz = 200;
        float start = o.Height(Cx, Cz);
        o.Lift(Cx, Cz, Bell(Cx, Cz, -25, 50));
        var plan = o.Save();
        Assert.True(plan.Discs > 0);
        var after = UpliftWorld.Of(w.Load());
        Assert.InRange(after.Height(Cx, Cz) - start, -26, -24);
    }

    [Fact]
    public void WithoutLiftThereIsNothingToDo()
    {
        using var w = new TempWorld();
        var o = UpliftWorld.Of(w.Load());
        Assert.Null(Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId));
    }
}
