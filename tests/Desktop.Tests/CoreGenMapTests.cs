using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

// The world map's data (MapData) and the terrain service it reads: the 1 m close-up with the saved
// and pending edits (the game's 8 m limit, paint), windows across zone borders, the close-up cache,
// the layers' byte layout, and the overview picture.
[Collection("World files")]
public class CoreGenMapTests
{
	private static (WorldSave World, TerrainService Terrain, EditStore Edits) Open(TempWorld w)
	{
		var world = w.Load();
		return (world, new TerrainService(world, null, overviewSize: 64), new EditStore(world));
	}

	private static float H(MapData.Layers l, int col, int row) => (float)l.Heights[row * l.Width + col];

	[Fact]
	public void TheCloseUpIsTheBaseGroundWhereNothingIsEdited()
	{
		using var w = new TempWorld();
		var (_, terrain, edits) = Open(w);
		var map = new MapData(terrain, edits);
		// A zone the test world never edited: zone 5, 5 (x and z from 288 to 352).
		var l = map.Detail(288, 288, 64, edits.Version);
		var b = terrain.BaseZone(5, 5);
		for (int row = 0; row < 64; row += 7)
		{
			for (int col = 0; col < 64; col += 7)
			{
				Assert.Equal((float)(Half)b[row * EditStore.Grid + col], H(l, col, row));
			}
		}
		// Nothing painted: the paint layer is empty.
		Assert.All(Enumerable.Range(0, 64 * 64), i => Assert.Equal(0, l.Paint[i * 4 + 3]));
		// Every texel has a biome colour and a mask, opaque.
		Assert.All(Enumerable.Range(0, 64 * 64), i => Assert.Equal(255, l.Biome[i * 4 + 3]));
	}

	[Fact]
	public void TheCloseUpShowsEditsWithinTheGamesLimitAndPaint()
	{
		using var w = new TempWorld();
		var (_, terrain, edits) = Open(w);
		var e = new ZoneEdit(5, 5);
		int a = 10 * EditStore.Grid + 10, c = 20 * EditStore.Grid + 30, d = 40 * EditStore.Grid + 40;
		e.Modified[a] = true;
		e.Level[a] = 3f;
		// Level and smoothing together beyond 8 m: the game stops at 8 m.
		e.Modified[c] = true;
		e.Level[c] = 7f;
		e.Smooth[c] = 3f;
		e.Modified[d] = true;
		e.Level[d] = -7.5f;
		e.Smooth[d] = -1f;
		e.PaintModified[a] = true;
		e.Paint[a * 4] = 1f;
		e.Paint[a * 4 + 3] = 0.5f;
		edits.Put(e);
		var b = terrain.BaseZone(5, 5);
		var l = new MapData(terrain, edits).Detail(288, 288, 64, edits.Version);
		Assert.Equal((float)(Half)(b[a] + 3f), H(l, 10, 10));
		Assert.Equal((float)(Half)(b[c] + 8f), H(l, 30, 20));
		Assert.Equal((float)(Half)(b[d] - 8f), H(l, 40, 40));
		int p = (10 * 64 + 10) * 4;
		Assert.Equal(new byte[] { 255, 0, 0, 255 }, l.Paint[p..(p + 4)]);
		Assert.Equal(0, l.Paint[(11 * 64 + 10) * 4 + 3]);
	}

	[Fact]
	public void AWindowAcrossZoneBordersReadsEachZone()
	{
		using var w = new TempWorld();
		var (_, terrain, edits) = Open(w);
		// From x, z = 300 to 427: zones 5 and 6 on both axes (the border at 352).
		var l = new MapData(terrain, edits).Detail(300, 300, 128, 0);
		foreach (var (wx, wz) in new[] { (300, 300), (351, 351), (352, 352), (420, 310), (310, 420), (427, 427) })
		{
			int zx = (int)Math.Floor((wx + 32) / 64.0), zz = (int)Math.Floor((wz + 32) / 64.0);
			var b = terrain.BaseZone(zx, zz);
			int k = wz - (zz * 64 - 32), m = wx - (zx * 64 - 32);
			Assert.Equal((float)(Half)b[k * EditStore.Grid + m], H(l, wx - 300, wz - 300));
		}
		// Negative coordinates: zone -1 starts at -96.
		var neg = new MapData(terrain, edits).Detail(-96, -96, 64, 0);
		Assert.Equal((float)(Half)terrain.BaseZone(-1, -1)[0], H(neg, 0, 0));
	}

	[Fact]
	public void CloseUpsAreKeptPerEditVersionAndTheCacheStaysSmall()
	{
		using var w = new TempWorld();
		var (_, terrain, edits) = Open(w);
		var map = new MapData(terrain, edits);
		var first = map.Detail(288, 288, 64, 1);
		Assert.Same(first, map.Detail(288, 288, 64, 1));
		Assert.NotSame(first, map.Detail(288, 288, 64, 2));
		// More than six windows: the old ones are dropped and built again when asked.
		for (int i = 0; i < 7; i++)
		{
			map.Detail(288 + i * 64, 288, 64, 1);
		}
		Assert.NotSame(first, map.Detail(288, 288, 64, 1));
	}

	[Fact]
	public void LayersAreWrittenAsHalfFloatsThenThreeColourLayers()
	{
		var l = new MapData.Layers(2, 1);
		l.Set(0, 31.5f, (1, 2, 3), (4, 5, 6), null);
		l.Set(1, -2f, (7, 8, 9), (10, 11, 12), (13, 14, 15, 16));
		byte[] b = l.ToBytes();
		Assert.Equal(2 * 2 + 2 * 12, b.Length);
		Assert.Equal(31.5f, (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(b, 0)));
		Assert.Equal(-2f, (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(b, 2)));
		Assert.Equal(new byte[] { 1, 2, 3, 255, 7, 8, 9, 255 }, b[4..12]);
		Assert.Equal(new byte[] { 4, 5, 6, 255, 10, 11, 12, 255 }, b[12..20]);
		// Paint: only the painted texel, always opaque.
		Assert.Equal(new byte[] { 0, 0, 0, 0, 13, 14, 15, 255 }, b[20..28]);
	}

	[Fact]
	public void TheGlobalMapUsesTheGamesLayout()
	{
		using var w = new TempWorld();
		var (_, terrain, edits) = Open(w);
		var g = new MapData(terrain, edits).Global;
		Assert.Equal(MapData.GlobalSize, g.Width);
		Assert.Equal(MapData.GlobalSize, g.Height);
		// The corner is far out at sea: below the water, blue mask, no forest.
		Assert.True((float)g.Heights[0] < 30f);
		Assert.Equal(0, g.Mask[0]);
		// Somewhere near the middle is land with a known biome colour (opaque).
		int mid = (MapData.GlobalSize / 2) * MapData.GlobalSize + MapData.GlobalSize / 2;
		Assert.Equal(255, g.Biome[mid * 4 + 3]);
	}

	[Fact]
	public void TheOverviewIsAPngOfTheRequestedSize()
	{
		using var w = new TempWorld();
		var (_, terrain, _) = Open(w);
		using var png = SkiaSharp.SKBitmap.Decode(terrain.OverviewPng);
		Assert.Equal(64, png.Width);
		Assert.Equal(64, png.Height);
		// The corners are outside the round world: transparent; the middle is land or sea, opaque.
		Assert.Equal(0, png.GetPixel(0, 0).Alpha);
		Assert.Equal(255, png.GetPixel(32, 32).Alpha);
		Assert.Same(terrain.OverviewPng, terrain.OverviewPng);
	}

	[Fact]
	public void ZoneLevelHelpersAgreeWithEachOther()
	{
		using var w = new TempWorld();
		var (_, terrain, _) = Open(w);
		// Raw and base ground are the same where no location flattens it (no modifiers given).
		Assert.Equal(terrain.RawZone(5, 5), terrain.BaseZone(5, 5));
		// Vertex biomes and the zone's corner biomes come from the same generator.
		var vb = terrain.VertexBiomes(5, 5);
		Assert.Equal(65 * 65, vb.Length);
		Assert.Equal((int)terrain.BiomeAt(5 * 64 - 32, 5 * 64 - 32), vb[0]);
		Assert.Equal(4, terrain.CornerBiomes(5, 5).Length);
		// Corners beyond the world's edge are ocean.
		Assert.All(terrain.CornerBiomes(170, 170), b => Assert.Equal((int)Heightmap.Biome.Ocean, b));
		Assert.Equal(65 * 65 * 4, terrain.BaseMask(5, 5).Length);
	}
}
