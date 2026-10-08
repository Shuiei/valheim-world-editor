using ValheimGen;
using Xunit;
using Rnd = ValheimGen.UnityEngine.Random;

namespace WorldEditor.Tests;

// The small stand-ins the ported generator uses for Unity: vectors and their operators, Mathf, the
// random number generator at its edges (backwards ranges, the unit circle), DUtils' remapping, and
// the piece catalogue's footprints for the map.
[Collection("World files")]
public class CoreGenHelperTests
{
	[Fact]
	public void TwoDimensionalVectors()
	{
		Assert.Equal(new Vector2(0, 1), Vector2.up);
		Assert.Equal(new Vector2(0, -1), Vector2.down);
		Assert.Equal(new Vector2(-1, 0), Vector2.left);
		Assert.Equal(new Vector2(1, 0), Vector2.right);
		Assert.Equal(new Vector2(-2, 3), -new Vector2(2, -3));
		Assert.Equal(new Vector2(4, 6), 2f * new Vector2(2, 3));
		Assert.True(new Vector2(1, 2) != new Vector2(1, 3));
		Assert.False(new Vector2(1, 2) != new Vector2(1, 2));
		Assert.True(new Vector2(1, 2).Equals((object)new Vector2(1, 2)));
		Assert.False(new Vector2(1, 2).Equals("not a vector"));
		Assert.Equal(new Vector2(1, 2).GetHashCode(), new Vector2(1, 2).GetHashCode());
	}

	[Fact]
	public void ThreeDimensionalVectors()
	{
		var v = new Vector3(3, 0, 4);
		Assert.Equal(5f, v.magnitude);
		var n = v.normalized;
		Assert.Equal(0.6f, n.x, 5);
		Assert.Equal(0.8f, n.z, 5);
		// A vector too short to have a direction normalizes to zero.
		var z = new Vector3(0, 1e-6f, 0).normalized;
		Assert.Equal(0f, z.x + z.y + z.z);
		Assert.Equal(0f, Vector3.zero.magnitude);
		var s = new Vector3(1, 2, 3) + new Vector3(1, 1, 1) - new Vector3(0, 1, 0);
		Assert.Equal((2f, 2f, 4f), (s.x, s.y, s.z));
		var d = new Vector3(2, 4, 6) / 2f;
		Assert.Equal((1f, 2f, 3f), (d.x, d.y, d.z));
	}

	[Fact]
	public void IntegerVectors()
	{
		Assert.True(new Vector2i(1, 2) == new Vector2i(1, 2));
		Assert.True(new Vector2i(1, 2) != new Vector2i(2, 1));
		Assert.True(new Vector2i(1, 2).Equals((object)new Vector2i(1, 2)));
		Assert.False(new Vector2i(1, 2).Equals((object)7));
		var a = new Vector2s(5, 7);
		var b = new Vector2s(2, 3);
		Assert.Equal(new Vector2s(3, 4), a - b);
		Assert.True(a.Equals((object)new Vector2s(5, 7)));
		Assert.False(a.Equals((object)b));
		Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
	}

	[Fact]
	public void MathfLikeUnity()
	{
		Assert.Equal(2, Mathf.Min(2, 5));
		Assert.Equal(2f, Mathf.Min(2f, 5f));
		Assert.Equal(5, Mathf.Max(2, 5));
		Assert.Equal(3, Mathf.Clamp(9, 0, 3));
		Assert.Equal(0, Mathf.Clamp(-9, 0, 3));
		Assert.Equal(2, Mathf.Clamp(2, 0, 3));
		Assert.Equal(1f, Mathf.Cos(0f));
		Assert.Equal(0.5, DUtils.InverseLerp(10, 20, 15));
		// An empty range: 0, not a division by zero.
		Assert.Equal(0.0, DUtils.InverseLerp(10, 10, 15));
	}

	[Fact]
	public void RandomRangesBackwardsAndEmpty()
	{
		Rnd.InitState(42);
		for (int i = 0; i < 200; i++)
		{
			// Backwards: Unity gives a value in (max, min].
			int v = Rnd.Range(10, 3);
			Assert.InRange(v, 4, 10);
		}
		Assert.Equal(4, Rnd.Range(4, 4));
		for (int i = 0; i < 200; i++)
		{
			var c = Rnd.insideUnitCircle;
			Assert.True(c.x * c.x + c.y * c.y <= 1.0001f);
		}
		// The same seed, the same numbers.
		Rnd.InitState(7);
		int first = Rnd.Range(0, 1000);
		Rnd.InitState(7);
		Assert.Equal(first, Rnd.Range(0, 1000));
	}

	[Fact]
	public void TheMapsFootprintsLeaveOutDeletedAndUnknownPieces()
	{
		var world = new TerrainEditor.Save.WorldSave { Directory = "none", SaveNumber = 1 };
		Assert.True(TerrainEditor.Terrain.PieceCatalog.Count > 100);
		int wall = TerrainEditor.Save.StableHash.Of("wood_wall_half");
		world.Pieces.Add((5, wall, new System.Numerics.Vector3(10, 30, 20), 90f));
		world.Pieces.Add((6, wall, new System.Numerics.Vector3(12, 30, 20), 0f));
		// A piece the catalogue does not know is left out.
		world.Pieces.Add((7, TerrainEditor.Save.StableHash.Of("not_a_piece"), new System.Numerics.Vector3(1, 2, 3), 0f));
		const int S = TerrainEditor.Terrain.PieceCatalog.Stride;
		var all = TerrainEditor.Terrain.PieceCatalog.Encode(world, Array.Empty<int>());
		Assert.Equal(2 * S, all.Length);
		var info = TerrainEditor.Terrain.PieceCatalog.Get(wall)!;
		// Position, turn, footprint, top, category, bottom, index in the names, id.
		Assert.Equal(new[] { 10f, 30f, 20f, 90f, info.MinX, info.MaxX, info.MinZ, info.MaxZ, 30 + info.MaxY, info.Category, 30 + info.MinY, info.Index, 5f }, all[..S]);
		Assert.Equal("wood_wall_half", TerrainEditor.Terrain.PieceCatalog.Names[info.Index]);
		// One deleted: left out.
		var less = TerrainEditor.Terrain.PieceCatalog.Encode(world, new[] { 5 });
		Assert.Equal(S, less.Length);
		Assert.Equal(6f, less[S - 1]);
	}

	[Fact]
	public void TheOverviewColoursWaterByDepthAndUnknownGroundGrey()
	{
		Assert.Equal((112f, 158f, 72f), TerrainService.BiomeColor(Heightmap.Biome.Meadows, 31));
		// The water line itself is dry ground; under it the biome does not matter and deeper is darker.
		Assert.Equal((112f, 158f, 72f), TerrainService.BiomeColor(Heightmap.Biome.Meadows, TerrainService.WaterLevel));
		Assert.Equal((37.5f, 89.5f, 135f), TerrainService.BiomeColor(Heightmap.Biome.Meadows, TerrainService.WaterLevel - 6));
		Assert.Equal((15f, 40f, 90f), TerrainService.BiomeColor(Heightmap.Biome.Plains, -100));
		// Ocean ground above the water line (a shore) and no biome at all have no colour of their own.
		Assert.Equal((150f, 150f, 150f), TerrainService.BiomeColor(Heightmap.Biome.Ocean, 31));
		Assert.Equal((150f, 150f, 150f), TerrainService.BiomeColor(Heightmap.Biome.None, 31));
	}

	[Fact]
	public void TheAlternateBiomeMapIsNeverReady()
	{
		// The port has no alternate biome map: the generator asking for a sector gets the empty answer.
		var data = new AltBiomeWorldData();
		Assert.False(data.IsReady);
		Assert.Null(data.PointSectors);
	}
}
