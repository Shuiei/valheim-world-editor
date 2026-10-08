using System.Numerics;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

// The game's runtime terrain modifiers (TerrainModifiers, Heightmap.ApplyModifiers) on a flat 30 m
// zone: the hoe's raise and dig (round and square, the 8 m limit of what players do), cultivating
// (smoothing kept within 1 m of the levelled ground), a location's flattening and its rotation,
// which zones a modifier reaches, the game's order, and what does not count as a modifier.
public class CoreGenModifierTests
{
	private const int N = 65;

	private static WorldSave World(params PlacedObject[] placed)
	{
		var w = new WorldSave { Directory = "none", SaveNumber = 1 };
		w.Placed.AddRange(placed);
		return w;
	}

	private static PlacedObject Net(string prefab, float x, float y, float z, long time = 0) => new(StableHash.Of(prefab), 0, new Vector3(x, y, z), Vector3.Zero, time);

	private static PlacedObject Loc(string location, float x, float y, float z, float turn = 0) => new(0, StableHash.Of(location), new Vector3(x, y, z), new Vector3(0, turn, 0), 0);

	private static float[] Flat() => Enumerable.Repeat(30f, N * N).ToArray();

	// The vertex of zone 0, 0 at world x, z (the zone spans -32..32).
	private static float At(float[] h, int x, int z) => h[(z + 32) * N + x + 32];

	[Fact]
	public void TheHoesRaiseLevelsARoundSpotAndKeepsTheGroundBefore()
	{
		var m = new TerrainModifiers(World(Net("raise", 0, 31, 0)));
		Assert.Equal(1, m.Count);
		var h = Flat();
		float[] before = m.Apply(0, 0, h);
		// Raise: half a metre above where it was placed, within 1.5 m (a round area).
		Assert.Equal(31.5f, At(h, 0, 0));
		Assert.Equal(31.5f, At(h, 1, 1));
		Assert.Equal(30f, At(h, 2, 0));
		Assert.Equal(30f, At(h, 2, 2));
		// The saved edits are clamped against the ground before the players' modifiers.
		Assert.All(before, v => Assert.Equal(30f, v));
	}

	[Fact]
	public void WhatPlayersDoStaysWithinEightMetres()
	{
		var m = new TerrainModifiers(World(Net("raise", 0, 60, 0), Net("digg", 10, 0, 10, time: 5)));
		var h = Flat();
		m.Apply(0, 0, h);
		Assert.Equal(38f, At(h, 0, 0));
		Assert.Equal(22f, At(h, 10, 10));
	}

	[Fact]
	public void TheNewDigIsSquare()
	{
		var m = new TerrainModifiers(World(Net("digg_v2", 0, 30, 0)));
		var h = Flat();
		m.Apply(0, 0, h);
		// One metre down over a square of 3 × 3 vertices, corners included.
		Assert.Equal(29f, At(h, 1, 1));
		Assert.Equal(29f, At(h, -1, 1));
		Assert.Equal(30f, At(h, 2, 0));
	}

	[Fact]
	public void CultivatingSmoothsWithinAMetreOfTheLevelledGround()
	{
		var m = new TerrainModifiers(World(Net("cultivate", 0, 35, 0)));
		var h = Flat();
		m.Apply(0, 0, h);
		// It pulls towards 35 m but a player's smoothing stays within 1 m of the ground under it.
		Assert.Equal(31f, At(h, 0, 0));
		Assert.InRange(At(h, 2, 0), 30f, 31f);
		Assert.Equal(30f, At(h, 4, 0));
	}

	[Fact]
	public void ALocationSmoothsItsGroundTowardsItsHeight()
	{
		// The goblin hut smooths the ground towards its height over 7 m (the game's cubic falloff);
		// it does not level.
		var m = new TerrainModifiers(World(Loc("GoblinHut02", 0, 33, 0)));
		Assert.Equal(1, m.LocationsWithModifiers);
		var h = Flat();
		float[] before = m.Apply(0, 0, h);
		Assert.Equal(33f, At(h, 0, 0));
		float t = 1f - MathF.Pow(3f / 7f, 3f);
		Assert.Equal(30f + 3f * t, At(h, 3, 0), 4);
		// Beyond 7 m (the corner at 5, 5 is 7.07 m away): untouched.
		Assert.Equal(30f, At(h, 5, 5));
		Assert.Equal(30f, At(h, 20, 20));
		// No player modifier: the edits are clamped against the smoothed ground itself.
		Assert.Equal(33f, At(before, 0, 0));
	}

	[Fact]
	public void ALocationThatLevelsSetsItsHeightInARoundArea()
	{
		// The statue group levels a 1.5 m circle 1 m above its height, smoothing out to 4 m.
		var m = new TerrainModifiers(World(Loc("Mistlands_StatueGroup1", 0, 32, 0)));
		var h = Flat();
		m.Apply(0, 0, h);
		Assert.Equal(33f, At(h, 0, 0));
		Assert.Equal(33f, At(h, 1, 1));
		Assert.InRange(At(h, 2, 0), 30f, 33f);
	}

	[Fact]
	public void ALocationsModifiersTurnWithIt()
	{
		// The goblin hut's second modifier sits 4.1 m to its west; turned 90°, it is to the north
		// or south instead. The heights there differ from the unturned hut's.
		var straight = Flat();
		new TerrainModifiers(World(Loc("GoblinHut02", 0, 33, 0))).Apply(0, 0, straight);
		var turned = Flat();
		new TerrainModifiers(World(Loc("GoblinHut02", 0, 33, 0, turn: 90))).Apply(0, 0, turned);
		Assert.NotEqual(straight, turned);
		// The middle is levelled the same.
		Assert.Equal(At(straight, 0, 0), At(turned, 0, 0));
	}

	[Fact]
	public void AModifierNearABorderReachesTheZonesItTouches()
	{
		var m = new TerrainModifiers(World(Net("raise", 31, 31, 31)));
		Assert.Single(m.InZone(0, 0));
		Assert.Single(m.InZone(1, 1));
		Assert.Single(m.InZone(1, 0));
		Assert.Empty(m.InZone(2, 2));
		Assert.Empty(m.InZone(-1, -1));
		// In the next zone, its vertices are levelled too (the zone shares that edge).
		var h = Flat();
		m.Apply(1, 1, h);
		Assert.Equal(31.5f, h[0]);
	}

	[Fact]
	public void PlayersModifiersComeLastThenByOrderTimeAndDistance()
	{
		var m = new TerrainModifiers(World(Net("raise", 0, 31, 0, time: 9), Loc("GoblinHut02", 0, 33, 0), Net("digg", 0, 31, 0, time: 2)));
		var list = m.InZone(0, 0);
		// The location's two modifiers (order 0, then 1), then the players' by time.
		Assert.False(list[0].Player);
		Assert.False(list[1].Player);
		Assert.True(list[0].SortOrder <= list[1].SortOrder);
		Assert.True(list[2].Player && list[2].LevelOffset < 0);
		Assert.True(list[3].Player && list[3].LevelOffset > 0);
	}

	[Fact]
	public void ObjectsThatAreNotModifiersAreLeftOut()
	{
		var m = new TerrainModifiers(World(
			Net("Beech1", 0, 30, 0),
			// A location's name placed as an object, and an object's name as a location: neither counts.
			Net("GoblinHut02", 0, 30, 0),
			Loc("raise", 0, 30, 0)));
		Assert.Equal(0, m.Count);
		Assert.Equal(0, m.LocationsWithModifiers);
		var h = Flat();
		Assert.All(m.Apply(0, 0, h), v => Assert.Equal(30f, v));
		Assert.All(h, v => Assert.Equal(30f, v));
	}

	[Fact]
	public void TheRadiusIsTheLargestOfWhatTheModifierDoes()
	{
		var m = new TerrainModifiers.Modifier(Vector3.Zero, true, 0, 2, false, true, 5, 3, true, 7, 0, false, 0);
		Assert.Equal(7, m.Radius);
		Assert.Equal(5, (m with { PaintCleared = false }).Radius);
		Assert.Equal(2, (m with { PaintCleared = false, Smooth = false }).Radius);
		Assert.Equal(0, (m with { PaintCleared = false, Smooth = false, Level = false }).Radius);
		// The network prefabs the save reader needs to know about include the hoe's.
		Assert.Contains(StableHash.Of("raise"), TerrainModifiers.NetworkPrefabHashes);
		Assert.DoesNotContain(StableHash.Of("GoblinHut02"), TerrainModifiers.NetworkPrefabHashes);
	}
}
