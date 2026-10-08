using System.Numerics;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

// New objects built without a model in the world, and the prefab catalogue they rely on.
public class ObjectTests
{
	[Fact]
	public void CatalogueKnowsPlaceableKinds()
	{
		Assert.NotNull(PrefabCatalog.Get(Fixtures.Hash("sapling_turnip")));
		Assert.NotNull(PrefabCatalog.Get(Fixtures.Hash("Beech1")));
		// Creatures and item drops are not placeable.
		Assert.Null(PrefabCatalog.Get(Fixtures.Hash("Boar")));
		Assert.True(PrefabCatalog.Placeable.Count() > 1000);
	}

	[Fact]
	public void SaplingsKnowTheirGrowRadius()
	{
		var turnip = PrefabCatalog.Get(Fixtures.Hash("sapling_turnip"))!;
		Assert.Equal(0.5f, turnip.GrowRadius, 2);
		Assert.True(turnip.NeedsCultivated);
		Assert.Equal(0f, PrefabCatalog.Get(Fixtures.Hash("Beech1"))!.GrowRadius);
	}

	[Fact]
	public void BlankObjectHasTheGameFlagsAndNoData()
	{
		var info = PrefabCatalog.Get(Fixtures.Hash("Pickable_Mushroom_Magecap"))!;
		byte[] b = ZdoBuilder.Blank(Fixtures.Hash("Pickable_Mushroom_Magecap"), info.Flags, new Vector3(10, 40, -5), Vector3.Zero, 0f);
		ushort flags = BitConverter.ToUInt16(b, 0);
		Assert.True((flags & 0x100) != 0, "saved by the game (persistent)");
		Assert.Equal(0, flags & 0xFF);
		Assert.Equal(2 + 12 + 4, b.Length);
		Assert.Equal(10f, BitConverter.ToSingle(b, 2));
		Assert.Equal(Fixtures.Hash("Pickable_Mushroom_Magecap"), BitConverter.ToInt32(b, 14));
	}

	[Fact]
	public void BlankObjectStoresRotationAndScale()
	{
		var info = PrefabCatalog.Get(Fixtures.Hash("sapling_turnip"))!;
		byte[] b = ZdoBuilder.Blank(Fixtures.Hash("sapling_turnip"), info.Flags, Vector3.Zero, new Vector3(0, 45, 0), 1.25f);
		ushort flags = BitConverter.ToUInt16(b, 0);
		Assert.True((flags & 0x1000) != 0, "rotation");
		Assert.True((flags & 0x4) != 0, "a Vector3 section (scale)");
	}

	[Fact]
	public void StableHashMatchesTheGame()
	{
		// Known values from the game's own saves.
		Assert.Equal(-367065113, StableHash.Of("_TerrainCompiler"));
	}

	[Fact]
	public void PiecesKnowTheirSnapPoints()
	{
		var wall = PieceCatalog.Get(Fixtures.Hash("woodwall"))!;
		Assert.Equal(4, wall.Snaps.Length);
		// A 2 m wall: snap points at x = -1 and 1.
		Assert.Equal(2f, wall.Snaps.Max(p => p[0]) - wall.Snaps.Min(p => p[0]), 2);
		Assert.Contains(PieceCatalog.WithSnaps, i => i.Name == "wood_fence");
		Assert.True(PieceCatalog.WithSnaps.Count() > 200);
	}
}
