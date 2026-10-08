using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

// Runestones and tamed animals in the save: a runestone is a location proxy, listed as an object under
// its location's name (so it can be shown, picked and deleted); a creature with "tamed" set is its own
// View kind. The objects are saved into the test world through the editor's own writer first.
[Collection("World files")]
public class CoreRunestoneTamedTests
{
	private static readonly int Proxy = StableHash.Of("LocationProxy");
	private static readonly int Meadows = StableHash.Of("Runestone_Meadows");
	private static readonly int Boar = StableHash.Of("Boar");

	private static NewObject Make(int prefab, Vector3 at, params (string Key, int Value)[] ints)
	{
		var z = ZdoData.Parse(ZdoBuilder.Blank(prefab, 0x100, at, new Vector3(0, 30, 0), 0f));
		foreach (var (key, value) in ints)
		{
			z.Set("ints", StableHash.Of(key), value.ToString());
		}
		return new NewObject(-1, prefab, at, new Vector3(0, 30, 0), 0f, Fresh: false, Raw: z.Serialize());
	}

	private static WorldSave WithStoneAndBoars(TempWorld w)
	{
		var world = w.Load();
		var added = new[]
		{
			Make(Proxy, new Vector3(10, 30, 10), ("location", Meadows)),
			Make(Boar, new Vector3(14, 30, 10), ("tamed", 1)),
			Make(Boar, new Vector3(18, 30, 10)),
		};
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), added: added);
		Assert.True(r.Saved, r.Message);
		return w.Load();
	}

	[Fact]
	public void ARunestoneIsListedUnderItsLocationAndOnlyTamedBoarsAreAnimals()
	{
		using var w = new TempWorld();
		var world = WithStoneAndBoars(w);
		var stone = Assert.Single(world.Objects, o => o.Prefab == Meadows);
		Assert.Equal(10, stone.Position.X, 0.01f);
		Assert.Equal(Proxy, world.ObjectRefs[stone.Id].Prefab);
		Assert.Equal("Runestone_Meadows", PrefabCatalog.NameOf(Meadows));
		Assert.True(PrefabCatalog.IsRunestone(Meadows));
		Assert.Equal(ObjectKind.Runestones, ObjectKinds.Of("Runestone_Meadows", false));
		// Not something a new object can be built from: copies and pastes leave it out.
		Assert.False(world.CanCreate(Meadows));

		var boars = world.Objects.Where(o => o.Prefab == Boar).ToList();
		Assert.Equal(2, boars.Count);
		var tame = Assert.Single(boars, b => world.Tamed.Contains(b.Id));
		Assert.Equal(14, tame.Position.X, 0.01f);
		Assert.Equal(ObjectKind.Animals, ObjectKinds.Of("Boar", false, tamed: true));
		Assert.Equal(ObjectKind.Other, ObjectKinds.Of("Boar", false));
	}

	[Fact]
	public void DeletingARunestoneRemovesItsProxyFromTheSave()
	{
		using var w = new TempWorld();
		var world = WithStoneAndBoars(w);
		int stone = world.Objects.Single(o => o.Prefab == Meadows).Id;
		int before = world.ObjectRefs.Count;
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), deleted: new[] { stone });
		Assert.True(r.Saved, r.Message);
		world = w.Load();
		Assert.DoesNotContain(world.Objects, o => o.Prefab == Meadows);
		Assert.DoesNotContain(world.Locations, l => l.Location == Meadows);
		Assert.Equal(before - 1, world.ObjectRefs.Count);
		// The tamed boar is still tamed after the save.
		Assert.Single(world.Objects, o => o.Prefab == Boar && world.Tamed.Contains(o.Id));
	}
}
