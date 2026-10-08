using System.Numerics;
using System.Reflection;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// Blueprint files of other mods at their edges (old comma decimals, other mods' sections, pieces seen
// from straight above, terrain only, square and round terrain with paint), and "Regrow nature" over
// zones of every biome of the test world's map: always the same draws, inside the zones, on the ground
// (or the water), Unity's random state put back, and Unity's LookRotation in each of its cases.
public class CoreAppFormatTests
{
	[Fact]
	public void OldPlanBuildFilesWithCommaDecimalsAreRead()
	{
		var bp = BlueprintFormats.Parse("old.blueprint", "#Pieces\nwood_floor;Building;1,5;0,25;-2,5;0;0;0;1;;1;1;1\n");
		var p = Assert.Single(bp.Pieces);
		Assert.Equal(new Vector3(1.5f, 0.25f, -2.5f), p.Position);
		var v = BlueprintFormats.Parse("old.vbuild", "woodwall 0 0 0 1 1,5 0 -2,25\n");
		Assert.Equal(new Vector3(1.5f, 0, -2.25f), Assert.Single(v.Pieces).Position);
	}

	[Fact]
	public void OtherModsSectionsAreSkippedUntilAKnownOne()
	{
		var bp = BlueprintFormats.Parse("mixed.blueprint", string.Join("\n",
			"#Name:", "#TerrainHeight", "1;2;3", "this is not a piece", "#Pieces", "wood_floor;Building;0;0;0;0;0;0;1", "﻿", "  "));
		// An empty #Name keeps the file's name.
		Assert.Equal("mixed", bp.Name);
		Assert.Single(bp.Pieces);
		Assert.Equal(0, bp.SkippedLines);
	}

	[Theory]
	[InlineData(90f)]
	[InlineData(-90f)]
	public void APieceSeenFromStraightAboveKeepsItsTurn(float pitch)
	{
		// Pitched a quarter turn and yawed 30°: only yaw and roll together are defined; the angles found
		// rebuild the same rotation.
		var q = BlueprintFormats.FromEuler(new Vector3(pitch, 30, 0));
		var e = BlueprintFormats.ToEuler(q);
		Assert.Equal((pitch + 360) % 360, e.X, 1);
		Assert.Equal(0f, e.Z, 3);
		var back = BlueprintFormats.FromEuler(e);
		Assert.True(MathF.Abs(Quaternion.Dot(q, back)) > 0.9999f, $"{e}");
	}

	[Fact]
	public void ABlueprintOfGroundOnlyIsLevelledAndPainted()
	{
		var bp = BlueprintFormats.Parse("ground.blueprint", string.Join("\n",
			"#Terrain", "square;0;10;0;2;45;0;paved", "circle;6;12;0;1.5;0;0;Cultivated", "circle;-6;11;0;1;0;0;dirt", "circle;0;9;8;1;0;0;grass"));
		var clip = BlueprintFormats.ToClip(bp, _ => true, out var unknown);
		Assert.Empty(unknown);
		Assert.Empty((JsonArray)clip["objects"]!);
		int w = (int)clip["w"]!, h = (int)clip["h"]!;
		var rel = ((JsonArray)clip["rel"]!).Select(v => (int)v!).ToArray();
		var pnt = ((JsonArray)clip["pnt"]!).Select(v => (int)v!).ToArray();
		Assert.True(w > 10 && h > 8);
		// Heights in cm above the lowest entry (9 m); points no entry covers have no ground.
		Assert.Contains(100, rel);
		Assert.Contains(300, rel);
		Assert.Contains(200, rel);
		Assert.Contains(0, rel);
		Assert.Contains(-32768, rel);
		// Paint: paved (blue), cultivated (green), dirt (red); "grass" is not a paint and leaves it alone.
		var paints = Enumerable.Range(0, w * h).Where(i => rel[i] != -32768).Select(i => (pnt[i * 4], pnt[i * 4 + 1], pnt[i * 4 + 2])).Distinct().ToList();
		Assert.Contains((0, 0, 255), paints);
		Assert.Contains((0, 255, 0), paints);
		Assert.Contains((255, 0, 0), paints);
		Assert.Contains((-255, -255, -255), paints);
	}

	[Fact]
	public void AnEmptyBlueprintIsAOnePointCopy()
	{
		var clip = BlueprintFormats.ToClip(BlueprintFormats.Parse("empty.vbuild", ""), _ => true, out _);
		Assert.Equal((1, 1), ((int)clip["w"]!, (int)clip["h"]!));
		Assert.Equal(-32768, (int)((JsonArray)clip["rel"]!)[0]!);
		Assert.Equal(4, ((JsonArray)clip["poly"]!).Count);
	}

	[Fact]
	public void WritingAnEmptyCopyGivesOnlyTheHeader()
	{
		var clip = new JsonObject { ["objects"] = new JsonArray() };
		Assert.Equal("", BlueprintFormats.Write(clip, "vbuild", "x", _ => 0));
		string planbuild = BlueprintFormats.Write(new JsonObject(), "blueprint", "Nothing", _ => 99);
		Assert.Contains("#Name:Nothing", planbuild);
		Assert.EndsWith("#Pieces\n", planbuild);
	}
}

// Regrow over many zones uses the world generator (static state): one world at a time.
[Collection("World files")]
public class CoreAppRegrowTests
{
	private static readonly MethodInfo LookRotation = typeof(Regrow).GetMethod("LookRotation", BindingFlags.NonPublic | BindingFlags.Static)!;

	[Theory]
	[InlineData(1f, 0f, 0f, 0f, 1f, 0f)]
	[InlineData(0f, 0f, -1f, 0f, 1f, 0f)]
	[InlineData(0f, 0f, -1f, 0f, -1f, 0f)]
	[InlineData(0f, 0f, 1f, 0f, -1f, 0f)]
	[InlineData(0.3f, 0.2f, 0.9f, 0.1f, 1f, 0.2f)]
	public void LookRotationTurnsForwardOntoTheGivenDirection(float fx, float fy, float fz, float ux, float uy, float uz)
	{
		var forward = Vector3.Normalize(new Vector3(fx, fy, fz));
		var up = new Vector3(ux, uy, uz);
		var q = (Quaternion)LookRotation.Invoke(null, new object[] { forward, up })!;
		Assert.Equal(1f, q.Length(), 3);
		var f = Vector3.Transform(Vector3.UnitZ, q);
		Assert.True(Vector3.Distance(f, forward) < 1e-3f, $"{f} for {forward}");
		// Up stays on the side of the given up (made square to forward).
		Assert.True(Vector3.Dot(Vector3.Transform(Vector3.UnitY, q), up) > 0, $"{q}");
	}

	[Fact]
	public void RegrowAcrossEveryBiomeIsRepeatableAndOnTheGround()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var terrain = new ValheimGen.TerrainService(world, null);
		var edits = new EditStore(world);
		Assert.True(Regrow.RuleCount > 100);
		// A few zones of each biome on a coarse grid over the map, and some on a biome's edge.
		var picks = new List<(int X, int Z)>();
		var perBiome = new Dictionary<ValheimGen.Heightmap.Biome, int>();
		int edges = 0;
		for (int zz = -150; zz <= 150 && picks.Count < 40; zz += 7)
		{
			for (int zx = -150; zx <= 150 && picks.Count < 40; zx += 7)
			{
				if (zx * zx + zz * zz > 155 * 155)
				{
					continue;
				}
				var corners = terrain.CornerBiomes(zx, zz);
				bool edge = corners.Distinct().Count() > 1;
				var b = terrain.BiomeAt(zx * 64f, zz * 64f);
				if (edge && edges < 6)
				{
					edges++;
					picks.Add((zx, zz));
				}
				else if (!edge && perBiome.GetValueOrDefault(b) < 3)
				{
					perBiome[b] = perBiome.GetValueOrDefault(b) + 1;
					picks.Add((zx, zz));
				}
			}
		}
		Assert.True(perBiome.Count >= 6, string.Join(", ", perBiome.Keys));
		var state = ValheimGen.UnityEngine.Random.state;
		int total = 0;
		foreach (var (zx, zz) in picks)
		{
			var spots = Regrow.Zones(terrain, edits, world.Seed, zx, zz, zx, zz);
			// Unity's random state is put back for the next user.
			Assert.Equal(state, ValheimGen.UnityEngine.Random.state);
			Assert.Equal(spots, Regrow.Zones(terrain, edits, world.Seed, zx, zz, zx, zz));
			foreach (var s in spots)
			{
				Assert.InRange(s.X, zx * 64f - 32f - 20f, zx * 64f + 32f + 20f);
				Assert.InRange(s.Z, zz * 64f - 32f - 20f, zz * 64f + 32f + 20f);
				Assert.True(s.Scale > 0);
				Assert.All(new[] { s.Rx, s.Ry, s.Rz }, a => Assert.True(float.IsFinite(a)));
			}
			total += spots.Count;
		}
		Assert.True(total > 100, $"{total} spots in {picks.Count} zones");
		// A different seed gives different nature.
		var (fx, fz) = picks.First(p => Regrow.Zones(terrain, edits, world.Seed, p.X, p.Z, p.X, p.Z).Count > 3);
		Assert.NotEqual(Regrow.Zones(terrain, edits, world.Seed, fx, fz, fx, fz), Regrow.Zones(terrain, edits, world.Seed + 1, fx, fz, fx, fz));
	}

	// Coasts: rocks that only grow in shallow water (when any do: the draws decide), groups spilling
	// over into another biome.
	[Fact]
	public void RegrowOnCoastsKeepsToShallowWaterAndItsBiome()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var terrain = new ValheimGen.TerrainService(world, null);
		var edits = new EditStore(world);
		var coasts = new List<(int X, int Z)>();
		for (int zz = -90; zz <= 90 && coasts.Count < 40; zz += 2)
		{
			for (int zx = -90; zx <= 90 && coasts.Count < 40; zx += 2)
			{
				var c = terrain.CornerBiomes(zx, zz);
				if (c.Contains((int)ValheimGen.Heightmap.Biome.Ocean) && c.Any(b => b != (int)ValheimGen.Heightmap.Biome.Ocean))
				{
					coasts.Add((zx, zz));
				}
			}
		}
		Assert.True(coasts.Count >= 20, $"{coasts.Count} coast zones");
		var all = coasts.SelectMany(z => Regrow.Zones(terrain, edits, world.Seed, z.X, z.Z, z.X, z.Z)).ToList();
		Assert.NotEmpty(all);
		var rocks = all.Where(s => s.Name == "rock4_coast").ToList();
		// Coast rocks stand between 2 m and 0.5 m below the sea.
		Assert.All(rocks, r => Assert.InRange(r.Y - ValheimGen.TerrainService.WaterLevel, -2.01f, -0.49f));
	}

	[Fact]
	public void SeveralZonesAtOnceAreTheZonesOneByOne()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var terrain = new ValheimGen.TerrainService(world, null);
		var edits = new EditStore(world);
		var together = Regrow.Zones(terrain, edits, world.Seed, 0, 0, 1, 0);
		var apart = Regrow.Zones(terrain, edits, world.Seed, 0, 0, 0, 0).Concat(Regrow.Zones(terrain, edits, world.Seed, 1, 0, 1, 0)).ToList();
		// The same spots; heights may differ in the last float digit (the ground is read from another corner).
		Assert.Equal(apart.Count, together.Count);
		for (int i = 0; i < apart.Count; i++)
		{
			Assert.Equal(apart[i] with { Y = 0 }, together[i] with { Y = 0 });
			Assert.Equal(apart[i].Y, together[i].Y, 3);
		}
	}
}
