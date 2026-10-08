using System.IO.Compression;
using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// What protects a world: every object reads and writes back exactly, a save changes nothing it was
// not asked to, the zone list survives a round trip, and broken inputs are refused with a reason.
public class CoreIntegrityTests
{
	[Fact]
	public void EveryObjectOfTheSaveReadsAndWritesBackExactly()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		Assert.True(world.ObjectRefs.Count > 100);
		for (int id = 0; id < world.ObjectRefs.Count; id++)
		{
			byte[] bytes = world.ObjectBytes(id);
			byte[] again = ZdoData.Parse(bytes).Serialize();
			int at = bytes.AsSpan().CommonPrefixLength(again);
			Assert.True(bytes.AsSpan().SequenceEqual(again), $"object {id} ({PrefabName(world.ObjectRefs[id].Prefab)}) changes when written back: {bytes.Length} bytes, then {again.Length}, first difference at {at}: {Convert.ToHexString(bytes.AsSpan(Math.Max(0, at - 8), Math.Min(24, bytes.Length - Math.Max(0, at - 8))))} vs {Convert.ToHexString(again.AsSpan(Math.Max(0, at - 8), Math.Min(24, again.Length - Math.Max(0, at - 8))))}");
		}
	}

	[Fact]
	public void EveryObjectOfALiveSnapshotReadsAndWritesBackExactly()
	{
		WorldSave live = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
		for (int id = 0; id < live.ObjectRefs.Count; id++)
		{
			byte[] bytes = live.ObjectBytes(id);
			Assert.True(bytes.AsSpan().SequenceEqual(ZdoData.Parse(bytes).Serialize()), $"live object {id} changes when written back");
		}
	}

	private static string PrefabName(int hash) => TerrainEditor.Terrain.PrefabCatalog.DisplayName(hash) ?? hash.ToString();

	[Fact]
	public void ASaveLeavesEveryOtherObjectByteForByte()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var t = world.TerrainZones[0];
		var tree = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
		var edit = new EditStore(world).Get(t.ZoneX, t.ZoneZ)!.Clone();
		edit.Modified[500] = true;
		edit.Level[500] = 1.25f;
		var planted = new NewObject(-1, Fixtures.Hash("Beech1"), tree.Position + new Vector3(3, 0, 1), Vector3.Zero, 1f);
		// What must stay: every object but the deleted tree and the edited zone's terrain object.
		int terrainId = world.ObjectRefs.FindIndex(o => o.IsTerrain && o.Zone == (t.ZoneX, t.ZoneZ));
		var kept = Enumerable.Range(0, world.ObjectRefs.Count).Where(i => i != tree.Id && i != terrainId).Select(world.ObjectBytes).ToList();
		var r = WorldWriter.Save(world, new[] { edit }, new[] { tree.Id }, new[] { planted });
		Assert.True(r.Saved, r.Message);
		var again = WorldSave.Load(w.Dir);
		var after = Enumerable.Range(0, again.ObjectRefs.Count).Select(again.ObjectBytes).Select(Convert.ToBase64String).GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());
		foreach (var bytes in kept.Select(Convert.ToBase64String))
		{
			Assert.True(after.TryGetValue(bytes, out int n) && n > 0, "an untouched object changed or went missing");
			after[bytes] = n - 1;
		}
		// What is left over: the new tree and the new terrain object.
		Assert.Equal(2, after.Values.Sum());
	}

	// A .db2 file's parts: version and time, the zone list package (unpacked) and what follows it.
	private static (int Version, double NetTime, byte[] Package, byte[] Tail) DbParts(string path)
	{
		using var r = new BinaryReader(File.OpenRead(path));
		int version = r.ReadInt32();
		double netTime = r.ReadDouble();
		byte[] packed = r.ReadBytes(r.ReadInt32());
		byte[] tail = r.ReadBytes((int)(r.BaseStream.Length - r.BaseStream.Position));
		using var gz = new System.IO.Compression.GZipStream(new MemoryStream(packed), System.IO.Compression.CompressionMode.Decompress);
		using var raw = new MemoryStream();
		gz.CopyTo(raw);
		return (version, netTime, raw.ToArray(), tail);
	}

	[Fact]
	public void TheZoneListSurvivesARoundTrip()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		string db2 = Path.Combine(w.Dir, $"_main.{world.SaveNumber}.db2");
		var db = ZoneDb.Load(db2);
		Assert.NotEmpty(db.Generated);
		string copy = Path.Combine(w.Dir, "copy.db2");
		db.Save(copy);
		// The same content: header, the zone list itself (compared unpacked: gzip's bytes depend on the
		// zlib the runtime has, and CI's packs the same data differently) and the rest of the file.
		var (a, b) = (DbParts(db2), DbParts(copy));
		Assert.Equal((a.Version, a.NetTime), (b.Version, b.NetTime));
		Assert.True(a.Package.AsSpan().SequenceEqual(b.Package), "the zone list changes when written back unchanged");
		Assert.True(a.Tail.AsSpan().SequenceEqual(b.Tail), "the rest of the file changes when written back");
		// A reset zone is no longer generated, and only that one.
		var zone = db.Generated.First();
		int count = db.Generated.Count;
		db.ResetZone(zone.X, zone.Z);
		Assert.DoesNotContain(zone, db.Generated);
		Assert.Equal(count - 1, db.Generated.Count);
	}

	[Fact]
	public void AFolderWithoutASaveIsRefused()
	{
		string dir = Path.Combine(Path.GetTempPath(), "vwe-empty-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(dir);
		try
		{
			var ex = Assert.Throws<InvalidDataException>(() => WorldSave.Load(dir));
			Assert.Contains("No _main.<n>.chunks file", ex.Message);
		}
		finally
		{
			Directory.Delete(dir, true);
		}
	}

	[Fact]
	public void AMissingChunkFileIsReported()
	{
		using var w = new TempWorld();
		var chunk = Directory.GetFiles(w.Dir, "*.chunk").First();
		File.Delete(chunk);
		var ex = Assert.Throws<FileNotFoundException>(() => WorldSave.Load(w.Dir));
		Assert.Contains("missing", ex.Message);
	}

	private static byte[] Gzip(byte[] raw)
	{
		using var ms = new MemoryStream();
		using (var gz = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
		{
			gz.Write(raw);
		}
		return ms.ToArray();
	}

	[Fact]
	public void ASnapshotThatIsNotOneIsRefused()
	{
		var ex = Assert.Throws<InvalidDataException>(() => WorldSave.LoadLive(Gzip(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }), "x"));
		Assert.Equal("not a WorldEditorBridge snapshot", ex.Message);
		// Not even gzip.
		Assert.ThrowsAny<Exception>(() => WorldSave.LoadLive(new byte[] { 0, 1, 2 }, "x"));
	}

	[Fact]
	public void ANewerSnapshotAsksForANewerEditor()
	{
		var raw = BitConverter.GetBytes(0x42455756).Concat(BitConverter.GetBytes(2)).ToArray();
		var ex = Assert.Throws<InvalidDataException>(() => WorldSave.LoadLive(Gzip(raw), "x"));
		Assert.Contains("update the editor", ex.Message);
	}

	// The game saves some objects (the terrain compiler) with a short position: kept while it fits.
	[Fact]
	public void AShortPositionIsKeptUntilItNoLongerFits()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		int id = world.ObjectRefs.FindIndex(o => o.IsTerrain);
		byte[] bytes = world.ObjectBytes(id);
		var z = ZdoData.Parse(bytes);
		Assert.True(z.SmallPosition);
		Assert.Equal(0f, z.Position.Y);
		// Moved by whole metres: still the short form, 8 bytes smaller than the full one.
		z.Position += new Vector3(64, 0, -64);
		byte[] moved = z.Serialize();
		Assert.Equal(bytes.Length, moved.Length);
		Assert.Equal(z.Position, ZdoData.Parse(moved).Position);
		// Off the whole metre, or lifted: the full form, read back exactly.
		foreach (var p in new[] { new Vector3(10.5f, 0, 3), new Vector3(10, 2, 3), new Vector3(40000, 0, 3) })
		{
			z.Position = p;
			byte[] full = z.Serialize();
			Assert.Equal(bytes.Length + 8, full.Length);
			var back = ZdoData.Parse(full);
			Assert.False(back.SmallPosition);
			Assert.Equal(p, back.Position);
		}
	}
}
