using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// A new world from a seed (WorldCreator, the docs world): the files the game writes, byte for byte
// where they can be; the seed's own ground; the zones around the middle generated with the game's
// vegetation; a world the editor opens, edits and saves; and never written over an existing world.
[Collection("World files")]
public sealed class CoreNewWorldTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-newworld-" + Guid.NewGuid().ToString("N")[..8]);

	// The test world's seed and unique id (tests/fixtures/CITest).
	private const string Seed = "GbXfypqU3G";
	private const long FixtureUid = 0x8d9ed32b;

	public void Dispose()
	{
		try
		{
			Directory.Delete(_dir, true);
		}
		catch (Exception)
		{
		}
	}

	private string Fixture(string file) => Path.Combine(EditorProcess.Fixtures(), "CITest", file);

	private string Folder(string name) => Path.Combine(_dir, name);

	[Fact]
	public void TheSeedNumberIsTheGames()
	{
		Assert.Equal(-1261093776, WorldCreator.SeedOf(Seed));
		Assert.Equal(WorldSave.Load(Path.Combine(EditorProcess.Fixtures(), "CITest")).Seed, WorldCreator.SeedOf(Seed));
	}

	[Fact]
	public void AnEmptyWorldIsSaveOneWithNothingInIt()
	{
		var made = WorldCreator.Create(Folder("Fresh"), " Fresh ", Seed, uid: 42);
		Assert.Equal((0, 0, -1261093776), (made.Zones, made.Objects, made.Seed));
		Assert.Equal(new[] { "_main.1.chunks", "_main.1.db2", "_main.1.fwl2", "_main.1.ok" }, Directory.GetFiles(Folder("Fresh")).Select(Path.GetFileName).Order().ToArray());
		Assert.Equal(BitConverter.GetBytes(41), File.ReadAllBytes(Path.Combine(Folder("Fresh"), "_main.1.ok")));
		var w = WorldSave.Load(Folder("Fresh"));
		Assert.Equal(1, w.SaveNumber);
		Assert.Equal(("Fresh", Seed, -1261093776, 2), (w.Name, w.SeedName, w.Seed, w.WorldGenVersion));
		Assert.Equal(0, w.ObjectCount);
		Assert.Equal(0, w.ChunkCount);
		Assert.Empty(w.TerrainZones);
		Assert.Empty(w.Locations);
		var db = w.Zones!;
		Assert.Empty(db.Generated);
		Assert.False(db.LocationsGenerated);
		Assert.Empty(db.Locations);
		Assert.Equal((41, 32, 0.0), (db.FileVersion, db.LocationVersion, db.NetTime));
	}

	[Fact]
	public void TheMetadataIsByteForByteTheGames()
	{
		// Same name, seed and unique id as the test world: the very same .fwl2.
		WorldCreator.Create(Folder("CITest"), "CITest", Seed, uid: FixtureUid);
		Assert.Equal(File.ReadAllBytes(Fixture("_main.2.fwl2")), File.ReadAllBytes(Path.Combine(Folder("CITest"), "_main.1.fwl2")));
	}

	[Fact]
	public void TheRestOfTheZoneFileIsTheGamesWithNoEventRunning()
	{
		WorldCreator.Create(Folder("Tail"), "Tail", Seed);
		byte[] Tail(string path)
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
	public void TheGroundIsTheSeedsOwn()
	{
		WorldCreator.Create(Folder("Ground"), "Ground", Seed);
		float[] made = new ValheimGen.TerrainService(WorldSave.Load(Folder("Ground"))).RawZone(5, -4);
		float[] game = new ValheimGen.TerrainService(WorldSave.Load(Path.Combine(EditorProcess.Fixtures(), "CITest"))).RawZone(5, -4);
		Assert.Equal(game, made);
		// Another seed, other ground.
		WorldCreator.Create(Folder("Other"), "Other", "AnotherOne");
		Assert.NotEqual(game, new ValheimGen.TerrainService(WorldSave.Load(Folder("Other"))).RawZone(5, -4));
	}

	[Fact]
	public void TheMiddleIsGeneratedWithTheGamesVegetation()
	{
		var made = WorldCreator.Create(Folder("Grown"), "Grown", Seed, radius: 1);
		Assert.Equal(9, made.Zones);
		Assert.True(made.Objects > 50, $"{made.Objects} objects");
		var w = WorldSave.Load(Folder("Grown"));
		Assert.Equal(made.Objects, w.ObjectCount);
		Assert.Equal(made.Objects, w.Chunks.Sum(c => c.IndexCount));
		Assert.Equal(Enumerable.Range(-1, 3).SelectMany(z => Enumerable.Range(-1, 3).Select(x => ((short)x, (short)z))).Order(), w.Zones!.Generated.Order());
		// Base chunk files (size 0, version 1), never the portals' chunk.
		Assert.All(w.Chunks, c => Assert.Equal((0, 1u), (c.Size, c.Version)));
		Assert.DoesNotContain(w.Chunks, c => c.Chunk == ChunkMath.PortalChunk);
		// Known kinds only, in or just around the generated zones (groups reach 20 m past theirs).
		Assert.All(w.Objects, o => Assert.NotNull(TerrainEditor.Terrain.PrefabCatalog.Get(o.Prefab)));
		Assert.All(w.Objects, o => Assert.InRange(o.Position.X, -96 - 20, 96 + 20));
		Assert.All(w.Objects, o => Assert.InRange(o.Position.Z, -96 - 20, 96 + 20));
		// The same seed grows the same world.
		WorldCreator.Create(Folder("Again"), "Grown", Seed, radius: 1, uid: 1);
		foreach (var c in w.Chunks)
		{
			Assert.Equal(File.ReadAllBytes(Path.Combine(Folder("Grown"), c.FileName)), File.ReadAllBytes(Path.Combine(Folder("Again"), c.FileName)));
		}
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
		var r = WorldWriter.Save(w, store.All().Where(z => z.Changed).ToList());
		Assert.True(r.Saved, r.Message);
		Assert.Equal((1, 1), (r.ZonesWritten, r.ZonesCreated));
		Assert.Contains(r.Skipped, m => m.StartsWith("zone 40, 40: not generated yet"));
		var after = WorldSave.Load(Folder("Edit"));
		Assert.Equal(2, after.SaveNumber);
		Assert.Equal(w.ObjectCount + 1, after.ObjectCount);
		Assert.Equal(new[] { (0, 0), (5, 5) }, after.TerrainZones.Select(z => (z.ZoneX, z.ZoneZ)).Order());
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
		Assert.DoesNotContain(true, zone.ModifiedHeight);
		Assert.DoesNotContain(true, zone.ModifiedPaint);
		// Its head (flags, short position, prefab, the TCData key) is the game's, as in the test world.
		byte[] game = File.ReadAllBytes(Fixture("20_20__1_1.chunk"));
		var src = WorldSave.Load(Path.Combine(EditorProcess.Fixtures(), "CITest")).TerrainZones[0].Source!;
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
		var r = WorldWriter.Save(w, store.All().Where(z => z.Changed).ToList());
		Assert.False(r.Saved);
		Assert.Equal(1, WorldSave.Load(Folder("Bare")).SaveNumber);
	}

	[Fact]
	public void TheNativeAppOpensIt()
	{
		WorldCreator.Create(Folder("Open"), "Open", Seed, radius: 1);
		var world = WorldSession.Open(Folder("Open"));
		var scene = WorldScene.Load(world, 0, 0, 1);
		Assert.NotEmpty(scene.Things);
		Assert.Equal("Open", world.World.Name);
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
		File.WriteAllBytes(Path.Combine(g, "20_20__0_1.chunk"), new byte[] { 41, 0, 0, 0, 0, 0 });
		Assert.Throws<InvalidOperationException>(() => WorldCreator.Create(g, "Other", "Other1"));
		// Other files do not stop it.
		string h = Folder("Notes");
		Directory.CreateDirectory(h);
		File.WriteAllText(Path.Combine(h, "readme.txt"), "");
		WorldCreator.Create(h, "Notes", Seed);
		Assert.Equal("Notes", WorldSave.Load(h).Name);
	}

	[Theory]
	[InlineData("", Seed, 0)]
	[InlineData("  ", Seed, 0)]
	[InlineData("a/b", Seed, 0)]
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
