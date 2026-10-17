using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Writing a planned save's files under a new number, and reading it back: the new save is listed, marked committed and
/// checked, and the world's files are left as they were.
/// </summary>
public class WorldWriterCommitTests
{
    [Fact]
    public void AnEmptyPlanCommitsANewNumberAndReadsItBack()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var plan = new WorldWriter.Planned([], [], []);

        var failure = WorldWriter.Commit(world, plan, []);

        Assert.Null(failure);
        Assert.Equal(Math.Max(world.SaveNumber, WorldWriter.LatestNumber(w.Dir)) + 1, plan.NewNumber);
        Assert.Equal(4, plan.NewFiles.Count);
        Assert.EndsWith($"_main.{plan.NewNumber}.ok", plan.NewFiles[3]);
        Assert.All(plan.NewFiles, f => Assert.True(File.Exists(f), f));
        Assert.NotNull(plan.Read);
        Assert.Equal(world.ObjectCount, plan.Read!.ObjectCount);
    }

    [Fact]
    public void AZoneResetIsWrittenIntoTheNewDatabase()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var t = world.TerrainZones[0];
        var plan = new WorldWriter.Planned([], [], []);

        var failure = WorldWriter.Commit(world, plan, [new ZoneReset(t.ZoneX, t.ZoneZ, KeepBuildings: true, Ground: false)]);

        Assert.Null(failure);
        var db = ZoneDb.Load(Path.Combine(w.Dir, $"_main.{plan.NewNumber}.db2"));
        Assert.DoesNotContain(((short)t.ZoneX, (short)t.ZoneZ), db.Generated);
    }
}
