using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What a finished save removes: the save it was made from (unless kept) and the files of an earlier save of the session.
/// </summary>
public class WorldWriterRemoveOldTests
{
    [Fact]
    public void TheSaveItWasMadeFromGoes()
    {
        using var w = new TempWorld();
        var world = w.Load();
        string basePath = Path.Combine(w.Dir, $"_main.{world.SaveNumber}.db2");
        Assert.True(File.Exists(basePath));
        var plan = new WorldWriter.Planned([], [], []);

        WorldWriter.RemoveOld(world, plan, new WorldWriter.Options());

        Assert.False(File.Exists(basePath));
    }

    [Fact]
    public void KeepBaseKeepsTheSaveItWasMadeFrom()
    {
        using var w = new TempWorld();
        var world = w.Load();
        string basePath = Path.Combine(w.Dir, $"_main.{world.SaveNumber}.db2");
        var plan = new WorldWriter.Planned([], [], []);

        WorldWriter.RemoveOld(world, plan, new WorldWriter.Options(KeepBase: true));

        Assert.True(File.Exists(basePath));
    }

    [Fact]
    public void ADroppedFileOfThisSessionGoes()
    {
        using var w = new TempWorld();
        var world = w.Load();
        string dropped = Path.Combine(w.Dir, "_main.999.db2");
        File.WriteAllText(dropped, "earlier save");
        var plan = new WorldWriter.Planned([], [], []);

        WorldWriter.RemoveOld(world, plan, new WorldWriter.Options(KeepBase: true, Drop: [dropped]));

        Assert.False(File.Exists(dropped));
    }

    [Fact]
    public void ADroppedFileThatIsPartOfTheNewSaveStays()
    {
        using var w = new TempWorld();
        var world = w.Load();
        string kept = Path.Combine(w.Dir, "_main.998.db2");
        File.WriteAllText(kept, "new save");
        var plan = new WorldWriter.Planned([], [], []);
        plan.NewFiles.Add(kept);

        WorldWriter.RemoveOld(world, plan, new WorldWriter.Options(KeepBase: true, Drop: [kept]));

        Assert.True(File.Exists(kept));
    }
}
