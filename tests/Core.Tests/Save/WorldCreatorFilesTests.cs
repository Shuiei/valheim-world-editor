using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A new world's files: the files the game writes, byte for byte where they can be, the world name and arguments that are
/// refused, and a world the editor edits and saves, or never writes over an existing one.
/// </summary>
[Collection("World files")]
public sealed class WorldCreatorFilesTests : WorldCreatorTestBase
{
    private static readonly string[] Expected1 = ["_main.1.chunks", "_main.1.db2", "_main.1.fwl2", "_main.1.ok"];
    // The test world's unique id (tests/fixtures/CITest).
    private const long FixtureUid = 0x8d9ed32b;

    /// <summary>
    /// A file of the test world in tests/fixtures/CITest.
    /// </summary>
    private static string Fixture(string file) => Path.Combine(Fixtures.Root, "CITest", file);

    [Fact]
    public void AnEmptyWorldIsSaveOneWithNothingInIt()
    {
        var made = WorldCreator.Create(Folder("Fresh"), " Fresh ", Seed, uid: 42);
        Assert.Equal((0, 0, -1261093776), (made.Zones, made.Objects, made.Seed));
        Assert.Equal(Expected1, Directory.GetFiles(Folder("Fresh")).Select(Path.GetFileName).Order().ToArray());
        // In the format Valheim 1.0.20 writes a new world in (42), as the index, the zone file and the metadata are.
        Assert.Equal(BitConverter.GetBytes(42), File.ReadAllBytes(Path.Combine(Folder("Fresh"), "_main.1.ok")));
        var w = WorldSave.Load(Folder("Fresh"));
        Assert.Equal((1, 42), (w.SaveNumber, w.SaveVersion));
        Assert.Equal(("Fresh", Seed, -1261093776, 2), (w.Name, w.SeedName, w.Seed, w.WorldGenVersion));
        Assert.Equal(0, w.ObjectCount);
        Assert.Equal(0, w.ChunkCount);
        Assert.Empty(w.TerrainZones);
        Assert.Empty(w.Locations);
        var db = w.Zones!;
        Assert.Empty(db.Generated);
        Assert.False(db.LocationsGenerated);
        Assert.Empty(db.Locations);
        Assert.Equal((42, 32, 0.0), (db.FileVersion, db.LocationVersion, db.NetTime));
    }

    [Fact]
    public void TheMetadataIsByteForByteTheGames()
    {
        // Same name, seed and unique id as the test world: the very same .fwl2 as Valheim 1.0.20 saves it.
        WorldCreator.Create(Folder("CITest"), "CITest", Seed, uid: FixtureUid);
        Assert.Equal(File.ReadAllBytes(Path.Combine(Fixtures.World42, "_main.3.fwl2")), File.ReadAllBytes(Path.Combine(Folder("CITest"), "_main.1.fwl2")));
    }

    [Fact]
    public void TheRestOfTheZoneFileIsTheGamesWithNoEventRunning()
    {
        WorldCreator.Create(Folder("Tail"), "Tail", Seed);
        static byte[] Tail(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            int packed = BitConverter.ToInt32(b, 12);
            return b[(16 + packed)..];
        }
        byte[] game = Tail(Fixture("_main.2.db2")), made = Tail(Path.Combine(Folder("Tail"), "_main.1.db2"));
        Assert.Equal(game.Length, made.Length);
        // The event timer differs (a new world's starts at 0); the rest is the same.
        Assert.Equal(0f, BitConverter.ToSingle(made, 0));
        Assert.Equal(game[4..], made[4..]);
    }

    [Fact]
    public void TheEditorEditsAndSavesTheGeneratedZones()
    {
        WorldCreator.Create(Folder("Edit"), "Edit", Seed, radius: 1);
        var w = WorldSave.Load(Folder("Edit"));
        var store = new EditStore(w);
        var e = new ZoneEdit(0, 0);
        for (int i = 0; i < 100; i++)
        {
            e.Modified[i] = true;
            e.Level[i] = 1.5f;
        }
        store.Put(e);
        // A zone not generated yet but inside a chunk file that exists gets its own terrain object (the
        // game generates the zone around it later); one in no chunk file is left for the game.
        var inChunk = new ZoneEdit(5, 5);
        inChunk.Modified[0] = true;
        inChunk.Level[0] = 1f;
        store.Put(inChunk);
        var outside = new ZoneEdit(40, 40);
        outside.Modified[0] = true;
        outside.Level[0] = 1f;
        store.Put(outside);
        var r = WorldWriter.Save(w, [.. store.All().Where(z => z.Changed)]);
        Assert.True(r.Saved, r.Message);
        Assert.Equal((1, 1), (r.ZonesWritten, r.ZonesCreated));
        Assert.Contains(r.Skipped, m => m.StartsWith("zone 40, 40: not generated yet", StringComparison.Ordinal));
        var after = WorldSave.Load(Folder("Edit"));
        Assert.Equal(2, after.SaveNumber);
        Assert.Equal(w.ObjectCount + 1, after.ObjectCount);
        Assert.Equal([(0, 0), (5, 5)], after.TerrainZones.Select(z => (z.ZoneX, z.ZoneZ)).Order());
        var zone = after.TerrainZones.Single(z => z.ZoneX == 0);
        Assert.Equal(1.5f, zone.LevelDelta[50]);
        Assert.Equal(("Edit", -1261093776), (after.Name, after.Seed));
    }

    [Fact]
    public void ZoneZeroHasAnUneditedTerrainObjectLikeTheGames()
    {
        WorldCreator.Create(Folder("Terrain"), "Terrain", Seed, radius: 1);
        var w = WorldSave.Load(Folder("Terrain"));
        var zone = Assert.Single(w.TerrainZones);
        Assert.Equal((0, 0), (zone.ZoneX, zone.ZoneZ));
        Assert.Equal(42, zone.Source!.File.WorldVersion);
        Assert.DoesNotContain(true, zone.ModifiedHeight);
        Assert.DoesNotContain(true, zone.ModifiedPaint);
        // Its head (flags, short position, prefab, the TCData key) is the game's, as in the test world.
        byte[] game = File.ReadAllBytes(Fixture("20_20__1_1.chunk"));
        var src = WorldSave.Load(Path.Combine(Fixtures.Root, "CITest")).TerrainZones[0].Source!;
        byte[] made = WorldCreator.EmptyTerrainObject();
        Assert.Equal(game[(int)src.Start..(int)src.DataStart], made[..(int)(src.DataStart - src.Start)]);
    }

    [Fact]
    public void AnEmptyWorldHasNothingTheWriterCanSaveInto()
    {
        WorldCreator.Create(Folder("Bare"), "Bare", Seed);
        var w = WorldSave.Load(Folder("Bare"));
        var store = new EditStore(w);
        var e = new ZoneEdit(0, 0);
        e.Modified[0] = true;
        e.Level[0] = 1f;
        store.Put(e);
        var r = WorldWriter.Save(w, [.. store.All().Where(z => z.Changed)]);
        Assert.False(r.Saved);
        Assert.Equal(1, WorldSave.Load(Folder("Bare")).SaveNumber);
    }

    [Fact]
    public void AnExistingWorldIsNeverWrittenOver()
    {
        string f = Folder("Kept");
        WorldCreator.Create(f, "Kept", Seed, uid: 7);
        byte[] before = File.ReadAllBytes(Path.Combine(f, "_main.1.fwl2"));
        Assert.Throws<InvalidOperationException>(() => WorldCreator.Create(f, "Other", "Other1"));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(f, "_main.1.fwl2")));
        // A chunk file alone is a world too.
        string g = Folder("Chunks");
        Directory.CreateDirectory(g);
        File.WriteAllBytes(Path.Combine(g, "20_20__0_1.chunk"), [41, 0, 0, 0, 0, 0]);
        Assert.Throws<InvalidOperationException>(() => WorldCreator.Create(g, "Other", "Other1"));
        // Other files do not stop it.
        string h = Folder("Notes");
        Directory.CreateDirectory(h);
        File.WriteAllText(Path.Combine(h, "readme.txt"), "");
        WorldCreator.Create(h, "Notes", Seed);
        Assert.Equal("Notes", WorldSave.Load(h).Name);
    }

    [Theory]
    [InlineData("Home", true)]
    [InlineData("Ünïcode wörld", true)]
    [InlineData("COM10", true)]
    [InlineData("Console", true)]
    [InlineData("", false)]
    [InlineData("a/b", false)]
    [InlineData("My:World?", false)]
    [InlineData("a\\b", false)]
    [InlineData("tab\there", false)]
    [InlineData("CON", false)]
    [InlineData("nul.txt", false)]
    [InlineData("com1", false)]
    [InlineData("Lpt9", false)]
    [InlineData("COM¹", false)]
    [InlineData("lpt².txt", false)]
    [InlineData("CONIN$", false)]
    [InlineData("conout$.db", false)]
    [InlineData("Name.", false)]
    [InlineData("Name ", false)]
    public void AWorldNameIsAFileNameOnEverySystem(string name, bool ok)
    {
        // Windows' rules, whatever the system here: / \ : * ? " < > |, control characters, device names (CON, COM1,
        // CONIN$, with any extension), a dot or a space at the end.
        Assert.Equal(ok, WorldCreator.IsWorldName(name));
    }

    [Theory]
    [InlineData("", Seed, 0)]
    [InlineData("  ", Seed, 0)]
    [InlineData("a/b", Seed, 0)]
    [InlineData("My:World?", Seed, 0)]
    [InlineData("CON", Seed, 0)]
    [InlineData("Name", "", 0)]
    [InlineData("Name", "ElevenChars", 0)]
    [InlineData("Name", Seed, -1)]
    [InlineData("Name", Seed, 17)]
    public void BadArgumentsWriteNothing(string name, string seed, int radius)
    {
        string f = Folder("Bad");
        Assert.ThrowsAny<ArgumentException>(() => WorldCreator.Create(f, name, seed, radius));
        Assert.False(Directory.Exists(f));
    }
}
