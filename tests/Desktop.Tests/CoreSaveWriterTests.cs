using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// The world writer's remaining edges: a world with no terrain object to copy for new ground, a chunk
// file name already taken, a terrain template in an unexpected layout, and a value that cannot read
// back the same (the read-back check restores the backup).
public class CoreSaveWriterTests
{
	private static Dictionary<string, byte[]> Files(string dir) => Directory.GetFiles(dir).ToDictionary(f => Path.GetFileName(f), File.ReadAllBytes);

	private static void AssertSame(Dictionary<string, byte[]> before, string dir)
	{
		var after = Files(dir);
		Assert.Equal(before.Keys.Order(), after.Keys.Order());
		foreach (var (name, bytes) in before)
		{
			Assert.True(bytes.AsSpan().SequenceEqual(after[name]), $"{name} changed");
		}
	}

	private static (int X, int Z) FreshZone(WorldSave world)
	{
		var t = world.TerrainZones[0];
		for (int r = 1; r < 6; r++)
		{
			for (int dz = -r; dz <= r; dz++)
			{
				for (int dx = -r; dx <= r; dx++)
				{
					int x = t.ZoneX + dx, z = t.ZoneZ + dz;
					if (ChunkMath.Find(world.Chunks, x, z) != null && world.TerrainZones.All(q => q.ZoneX != x || q.ZoneZ != z))
					{
						return (x, z);
					}
				}
			}
		}
		throw new InvalidOperationException("no fresh zone");
	}

	private static ZoneEdit Raised(int zx, int zz, float by, int point)
	{
		var e = new ZoneEdit(zx, zz);
		e.Modified[point] = true;
		e.Level[point] = by;
		return e;
	}

	[Fact]
	public void NewGroundNeedsATerrainObjectToCopy()
	{
		using var w = new TempWorld();
		var world = w.Load();
		var (x, z) = FreshZone(world);
		// As in a world nobody has dug in yet.
		world.TerrainZones.Clear();
		var before = Files(w.Dir);
		var r = WorldWriter.Save(world, new[] { Raised(x, z, 1, 100) });
		Assert.False(r.Saved);
		Assert.Contains(r.Skipped, s => s.Contains("no terrain object to copy yet"));
		AssertSame(before, w.Dir);
	}

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
		var r = WorldWriter.Save(world, new[] { Raised(t.ZoneX, t.ZoneZ, 1.5f, 500) });
		Assert.True(r.Saved, r.Message);
		Assert.True(File.Exists(Path.Combine(w.Dir, ChunkFile.NameFor(file.Chunk, file.Size, file.Version + 2))));
		Assert.Equal("not ours", File.ReadAllText(taken));
		Assert.Equal(1.5f, WorldSave.Load(w.Dir).TerrainZones.Single(q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ).LevelDelta[500], 4);
	}

	[Fact]
	public void ATerrainTemplateInAnUnexpectedLayoutIsRefusedBeforeWriting()
	{
		using var w = new TempWorld();
		var world = w.Load();
		var (x, z) = FreshZone(world);
		var t = world.TerrainZones[0];
		t.Source = t.Source! with { Flags = (ushort)(t.Source.Flags & ~0x2000) };
		var before = Files(w.Dir);
		var ex = Assert.Throws<InvalidOperationException>(() => WorldWriter.Save(world, new[] { Raised(x, z, 1, 100) }));
		Assert.Contains("Unexpected terrain object layout", ex.Message);
		AssertSame(before, w.Dir);
	}

	[Fact]
	public void AValueThatDoesNotReadBackTheSameRestoresTheBackup()
	{
		using var w = new TempWorld();
		var world = w.Load();
		var t = world.TerrainZones[0];
		var e = new EditStore(world).Get(t.ZoneX, t.ZoneZ)!.Clone();
		// Not a number never equals itself: the read-back check finds the zone different.
		e.Modified[500] = true;
		e.Level[500] = float.NaN;
		var before = Files(w.Dir);
		var r = WorldWriter.Save(world, new[] { e });
		Assert.False(r.Saved);
		Assert.StartsWith("The saved world did not read back correctly (zone", r.Message);
		Assert.Contains("differs at point 500", r.Message);
		Assert.Contains("Nothing changed", r.Message);
		AssertSame(before, w.Dir);
	}

	[Fact]
	public void PaintThatDoesNotReadBackTheSameRestoresTheBackup()
	{
		using var w = new TempWorld();
		var world = w.Load();
		var t = world.TerrainZones[0];
		var e = new EditStore(world).Get(t.ZoneX, t.ZoneZ)!.Clone();
		e.PaintModified[600] = true;
		e.Paint[600 * 4] = float.NaN;
		var before = Files(w.Dir);
		var r = WorldWriter.Save(world, new[] { e });
		Assert.False(r.Saved);
		Assert.Contains("paint differs at point 600", r.Message);
		AssertSame(before, w.Dir);
	}
}
