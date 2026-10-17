using System.Globalization;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.WorldWriterTestHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// The files the world writer leaves behind: a taken chunk file name is skipped, and saves in a row or an open world leave
/// only the latest files.
/// </summary>
public class WorldWriterFilesTests
{
    [Fact]
    public void ATakenChunkFileNameIsSkipped()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var t = world.TerrainZones[0];
        var file = t.Source!.File;
        // Something already uses the next version's name (a crashed save, say): the writer goes past it.
        string taken = Path.Combine(w.Dir, ChunkFile.NameFor(file.Chunk, file.Size, file.Version + 1));
        File.WriteAllText(taken, "not ours");
        var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, 1.5f, 500)]);
        Assert.True(r.Saved, r.Message);
        Assert.True(File.Exists(Path.Combine(w.Dir, ChunkFile.NameFor(file.Chunk, file.Size, file.Version + 2))));
        Assert.Equal("not ours", File.ReadAllText(taken));
        Assert.Equal(1.5f, WorldSave.Load(w.Dir).TerrainZones.Single(q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ).LevelDelta[500], 4);
    }

    [Fact]
    public void SavesInARowLeaveOnlyTheLatestFiles()
    {
        using var w = new TempWorld();
        var t = w.Load().TerrainZones[0];
        for (int n = 0; n < 3; n++)
        {
            WorldSave world = w.Load();
            var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, n + 1, 500 + n)]);
            Assert.True(r.Saved, r.Message);
            var again = WorldSave.Load(w.Dir);
            Assert.Equal(world.SaveNumber + 1, again.SaveNumber);
            // Only the new save number's files, and each chunk file once (the replaced version is gone).
            var names = Directory.GetFiles(w.Dir).Select(Path.GetFileName).ToList();
            Assert.All(names.Where(f => f!.StartsWith("_main.", StringComparison.Ordinal)), f => Assert.StartsWith($"_main.{again.SaveNumber}.", f));
            Assert.Equal(4, names.Count(f => f!.StartsWith("_main.", StringComparison.Ordinal)));
            var chunks = names.Where(f => f!.EndsWith(".chunk", StringComparison.Ordinal)).Select(f => f![..f!.LastIndexOf('_')]).ToList();
            Assert.Equal(chunks.Distinct().Count(), chunks.Count);
            Assert.Equal(n + 1f, again.TerrainZones.Single(q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ).LevelDelta[500 + n], 4);
        }
    }

    // An open world saving again and again (like Apply live): each save is made from the save it was
    // opened from, which stays; each earlier save of the session goes; leaving prunes down to the latest.
    [Fact]
    public void AnOpenWorldSavesFromItsBaseAndPrunesWhenLeft()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        int b = world.SaveNumber;
        var t = world.TerrainZones[0];
        IReadOnlyList<string>? last = null;
        for (int n = 0; n < 3; n++)
        {
            var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, n + 1, 500 + n)], options: new WorldWriter.Options(KeepBase: true, Drop: last));
            Assert.True(r.Saved, r.Message);
            last = r.Files;
            var saves = Directory.GetFiles(w.Dir, "_main.*.ok").Select(f => int.Parse(Path.GetFileName(f).Split('.')[1], CultureInfo.InvariantCulture)).Order().ToList();
            // The base and this save only.
            Assert.Equal(new[] { b, b + 1 + n }, saves);
            var again = WorldSave.Load(w.Dir);
            // The save as read back to check it comes with the result.
            Assert.Equal((r.Number, again.ObjectCount, again.Chunks.Count), (r.Read!.SaveNumber, r.Read.ObjectCount, r.Read.Chunks.Count));
            // Made from the base: only this save's raise, not the earlier ones.
            var zone = again.TerrainZones.Single(q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ);
            Assert.Equal(n + 1f, zone.LevelDelta[500 + n], 4);
            if (n > 0)
            {
                Assert.NotEqual(n + 0f, zone.LevelDelta[500 + n - 1], 4);
            }
        }
        Assert.Equal(b, WorldSave.Load(w.Dir).SaveNumber - 3);
        WorldWriter.Prune(w.Dir);
        var names = Directory.GetFiles(w.Dir).Select(Path.GetFileName).ToList();
        Assert.Equal(4, names.Count(f => f!.StartsWith("_main.", StringComparison.Ordinal)));
        Assert.All(names.Where(f => f!.StartsWith("_main.", StringComparison.Ordinal)), f => Assert.StartsWith($"_main.{b + 3}.", f));
        var after = WorldSave.Load(w.Dir);
        Assert.Equal(after.Chunks.Count, names.Count(f => f!.EndsWith(".chunk", StringComparison.Ordinal)));
    }
}
