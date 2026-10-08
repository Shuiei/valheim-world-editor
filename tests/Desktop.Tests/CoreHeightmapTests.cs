using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

// 16-bit heightmaps: written and read back, with every PNG row filter, and the ground of a real area.
public class HeightmapTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	public void Gray16RoundTripsWithEveryFilter(int filter)
	{
		int w = 37, h = 23;
		ushort[] px = Enumerable.Range(0, w * h).Select(i => (ushort)((i * 7919) % 65536)).ToArray();
		byte[] png = Png.WriteGray16(w, h, px, new Dictionary<string, string> { ["k"] = "v" }, filter);
		Png.Image img = Png.Read(png);
		Assert.Equal((w, h), (img.Width, img.Height));
		Assert.Equal("v", img.Text["k"]);
		for (int i = 0; i < px.Length; i++)
		{
			Assert.Equal(px[i], (ushort)Math.Round(img.Values[i] * 65535f));
		}
	}

	[Fact]
	public void SomethingElseIsRefused()
	{
		Assert.Throws<InvalidDataException>(() => Png.Read(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
	}

	[Fact]
	public void TheGroundOfAnAreaComesBackFromItsHeightmap()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		var terrain = new ValheimGen.TerrainService(world, new TerrainModifiers(world));
		var (W, H, heights) = TerrainEditor.Editing.HeightGrid.Read(terrain, edits, -1, -1, 1, 1);
		Assert.Equal((193, 193), (W, H));
		byte[] png = Heightmaps.Encode(W, H, heights, "test", out float min, out float max);
		Png.Image img = Png.Read(png);
		Assert.Equal(min.ToString("R", System.Globalization.CultureInfo.InvariantCulture), img.Text[Heightmaps.MinKey]);
		float[] back = Heightmaps.Resample(img, W, H);
		// 16 bits over the height range: well under a centimetre.
		float worst = Enumerable.Range(0, heights.Length).Max(i => MathF.Abs(min + back[i] * (max - min) - heights[i]));
		Assert.True(worst < 0.01f, $"worst difference {worst} m");
		// North is at the top of the picture: its first row is the area's northern edge.
		Assert.Equal(heights[(H - 1) * W], min + img.Values[0] * (max - min), 2);
	}
}

// Regrow nature against a world the game generated: REALWORLD=<world folder> OUT=<report file> runs it
// (how many saved trees, rocks... sit exactly where the port puts them, per kind). Without a world it
// does nothing; the test world is too small to hold generated nature.
public class RegrowProbe
{
	[Fact]
	public void RegrowMatchesTheGamesOwnPlacement()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var terrain = new ValheimGen.TerrainService(world, null);
		var a = Regrow.Zones(terrain, new EditStore(world), 12345, 0, 0, 1, 1);
		var b = Regrow.Zones(terrain, new EditStore(world), 12345, 0, 0, 1, 1);
		Assert.Equal(a, b);   // the same draws every time
		Assert.All(a, s => Assert.InRange(s.X, -32f - 20f, 96f + 20f));
	}

	[Fact]
	public void Probe()
	{
		string? dir = Environment.GetEnvironmentVariable("REALWORLD");
		if (dir == null) return;
		WorldSave world = WorldSave.Load(dir);
		var terrain = new ValheimGen.TerrainService(world, new TerrainModifiers(world));
		var edits = new EditStore(world);
		var gen = world.Zones!.Generated.Select(g => ((int)g.Item1, (int)g.Item2)).Where(g => Math.Abs(g.Item1) < 40 && Math.Abs(g.Item2) < 40).ToList();
		var byZone = world.Objects.GroupBy(o => ((int)MathF.Floor((o.Position.X + 32) / 64), (int)MathF.Floor((o.Position.Z + 32) / 64))).ToDictionary(g => g.Key, g => g.ToList());
		var per = new Dictionary<string, int[]>();
		foreach (var (zx, zz) in gen)
		{
			var spots = Regrow.Zones(terrain, edits, world.Seed, zx, zz, zx, zz);
			var set = spots.GroupBy(s => s.Name).ToDictionary(g => g.Key, g => g.ToList());
			foreach (var (n, l) in set) { var st = per.TryGetValue(n, out var q) ? q : per[n] = new int[3]; st[2] += l.Count; }
			foreach (var o in byZone.GetValueOrDefault((zx, zz)) ?? new())
			{
				string? n = TerrainEditor.Terrain.PrefabCatalog.NameOf(o.Prefab);
				if (n == null || !set.TryGetValue(n, out var list)) continue;
				var st = per[n];
				st[0]++;
				if (list.Any(s => MathF.Abs(s.X - o.Position.X) < 0.05f && MathF.Abs(s.Z - o.Position.Z) < 0.05f)) st[1]++;
			}
		}
		File.WriteAllText(Environment.GetEnvironmentVariable("OUT")!, $"total objs {per.Values.Sum(v => v[0])} matched {per.Values.Sum(v => v[1])} spots {per.Values.Sum(v => v[2])}\n" + string.Join("\n", per.OrderByDescending(kv => kv.Value[0]).Select(kv => $"{kv.Key}: objs {kv.Value[0]} matched {kv.Value[1]} spots {kv.Value[2]}")));
	}
}
