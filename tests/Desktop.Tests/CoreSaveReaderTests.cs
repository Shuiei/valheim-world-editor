using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// What the save reader takes from objects' values, with objects carrying them saved into a copy of
// the test world through the editor's own writer: a scale kept as one number, a location marker and
// its location, when a terrain modifier was made, the names of owners and players, a moved object
// given a new scale, and a zone list that cannot be read.
[Collection("World files")]
public class CoreSaveReaderTests
{
	// Saves objects made from scratch (their own bytes), then reads the world again.
	private static WorldSave SaveAndRead(TempWorld w, params ZdoData[] objects)
	{
		var world = w.Load();
		int n = 0;
		var added = objects.Select(z => new NewObject(--n, z.Prefab, z.Position, z.Rotation, 0f, Fresh: false, Raw: z.Serialize())).ToArray();
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), added: added);
		Assert.True(r.Saved, r.Message);
		return w.Load();
	}

	// An object at a spot of the test world (its first tree's place, moved by dx, dz).
	private static ZdoData At(TempWorld w, string prefab, float dx, float dz)
	{
		var tree = w.Load().Objects.First(o => o.Prefab == StableHash.Of("Beech1"));
		return new ZdoData { Prefab = StableHash.Of(prefab), Position = tree.Position + new Vector3(dx, 0, dz) };
	}

	[Fact]
	public void AScaleKeptAsOneNumberIsRead()
	{
		using var w = new TempWorld();
		var rock = At(w, "Rock_3", 2, 2);
		rock.Set("floats", StableHash.Of("scaleScalar"), "1.75");
		var world = SaveAndRead(w, rock);
		var o = world.Objects.Single(o => o.Prefab == rock.Prefab && Vector3.Distance(o.Position, rock.Position) < 0.01f);
		Assert.Equal(new Vector3(1.75f), o.Scale);
	}

	[Fact]
	public void ALocationMarkerGivesItsLocation()
	{
		using var w = new TempWorld();
		var marker = At(w, "LocationProxy", 3, 3);
		marker.Set("ints", WorldSave.LocationKey, StableHash.Of("GoblinHut02").ToString());
		var world = SaveAndRead(w, marker);
		Assert.Contains(world.Locations, l => l.Location == StableHash.Of("GoblinHut02") && Vector3.Distance(l.Position, marker.Position) < 0.01f);
		Assert.Contains(world.Placed, p => p.Location == StableHash.Of("GoblinHut02"));
		// A marker is neither an object to draw nor a piece.
		Assert.DoesNotContain(world.Objects, o => o.Prefab == WorldSave.LocationProxyPrefab);
	}

	[Fact]
	public void ATerrainModifierKeepsWhenItWasMade()
	{
		var before = WorldSave.ModifierPrefabs;
		try
		{
			WorldSave.ModifierPrefabs = TerrainEditor.Terrain.TerrainModifiers.NetworkPrefabHashes.ToHashSet();
			using var w = new TempWorld();
			var raise = At(w, "raise", 4, 4);
			raise.Set("longs", StableHash.Of("terrainModifierTimeCreated"), "638000000000000000");
			var bare = At(w, "digg", 6, 6);
			var world = SaveAndRead(w, raise, bare);
			Assert.Contains(world.Placed, p => p.Prefab == raise.Prefab && p.TimeCreated == 638000000000000000);
			// A modifier without any value is still placed (made at time zero).
			Assert.Contains(world.Placed, p => p.Prefab == bare.Prefab && p.TimeCreated == 0);
			Assert.DoesNotContain(world.Objects, o => o.Prefab == raise.Prefab || o.Prefab == bare.Prefab);
		}
		finally
		{
			WorldSave.ModifierPrefabs = before;
		}
	}

	[Fact]
	public void OwnersAndPlayersNamesAreRead()
	{
		using var w = new TempWorld();
		var ward = At(w, "guard_stone", 5, 5);
		ward.Set("longs", StableHash.Of("owner"), "111");
		ward.Set("strings", StableHash.Of("ownerName"), "Ada");
		var stone = At(w, "Player_tombstone", 7, 7);
		stone.Set("longs", StableHash.Of("playerID"), "222");
		stone.Set("strings", StableHash.Of("playerName"), "Bjorn");
		// A name without an id is not anyone's.
		var sign = At(w, "sign", 9, 9);
		sign.Set("strings", StableHash.Of("ownerName"), "Nobody");
		var world = SaveAndRead(w, ward, stone, sign);
		Assert.Equal("Ada", world.PlayerNames[111]);
		Assert.Equal("Bjorn", world.PlayerNames[222]);
		Assert.DoesNotContain("Nobody", world.PlayerNames.Values);
	}

	[Fact]
	public void AMovedObjectGivenANewScaleIsSavedWithIt()
	{
		using var w = new TempWorld();
		var world = w.Load();
		var tree = world.Objects.First(o => o.Prefab == StableHash.Of("Beech1"));
		var z = ZdoData.Parse(world.ObjectBytes(tree.Id));
		z.Set("floats", StableHash.Of("scaleScalar"), "3");
		var moved = new NewObject(-1, tree.Prefab, tree.Position + new Vector3(1, 0, 1), Vector3.Zero, 1.5f, Fresh: false, Raw: z.Serialize());
		var back = ZdoData.Parse(world.NewObjectBytes(moved, o => File.ReadAllBytes(Path.Combine(world.Directory, o.File.FileName)))!);
		Assert.Equal(new Vector3(1.5f), back.Vec3List.Single(v => v.Key == StableHash.Of("scale")).Value);
		Assert.DoesNotContain(back.FloatList, f => f.Key == StableHash.Of("scaleScalar"));
	}

	[Fact]
	public void AZoneListThatCannotBeReadLeavesTheRestOfTheWorld()
	{
		using var w = new TempWorld();
		var world = w.Load();
		File.WriteAllBytes(Path.Combine(w.Dir, $"_main.{world.SaveNumber}.db2"), new byte[] { 1, 2, 3 });
		var again = WorldSave.Load(w.Dir);
		Assert.Null(again.Zones);
		Assert.Equal(world.ObjectCount, again.ObjectCount);
	}

	[Fact]
	public void TerrainGridsKnowTheirWidth()
	{
		using var w = new TempWorld();
		var t = w.Load().TerrainZones[0];
		Assert.Equal(65, t.HeightWidth);
		Assert.Equal(65, t.PaintWidth);
	}
}
