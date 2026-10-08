using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;
using TerrainService = ValheimGen.TerrainService;

namespace WorldEditor.Tests;

// No limit ground (Uplift): ground lifted past the game's ±8 m becomes invisible ground discs (location
// proxies the game levels the ground for) plus ordinary edits. After saving, the ground the game makes
// from the save (generated ground, the discs, the edits clamped to ±8 m) must be the ground asked for.
[Collection("World files")]
public class CoreUpliftTests
{
	private sealed class Open
	{
		public required WorldSave World { get; init; }
		public required TerrainService Terrain { get; init; }
		public required EditStore Edits { get; init; }
		private int _next = -1;
		public int NextId() => _next--;

		public static Open Of(WorldSave world)
		{
			var mods = new TerrainModifiers(world);
			return new Open { World = world, Terrain = new TerrainService(world, mods), Edits = new EditStore(world) };
		}

		// The ground as the editor shows it (edits clamped to ±8 m, plus any lift not saved yet).
		public float Height(float x, float z)
		{
			int zx = (int)MathF.Floor((x + 32) / 64), zz = (int)MathF.Floor((z + 32) / 64);
			int i = ((int)MathF.Round(z) - (zz * 64 - 32)) * 65 + (int)MathF.Round(x) - (zx * 64 - 32);
			float b = Terrain.BaseZone(zx, zz)[i];
			var e = Edits.Get(zx, zz);
			return b + (e != null && e.Modified[i] ? Math.Clamp(e.Level[i] + e.Smooth[i], -8, 8) : 0) + (e?.Lift[i] ?? 0);
		}

		// Lifts the ground by f(x, z) (No limit) over the zones around (cx, cz).
		public void Lift(float cx, float cz, Func<float, float, float> f)
		{
			int zc = (int)MathF.Floor((cx + 32) / 64), zr = (int)MathF.Floor((cz + 32) / 64);
			for (int zz = zr - 2; zz <= zr + 2; zz++)
			{
				for (int zx = zc - 2; zx <= zc + 2; zx++)
				{
					var e = Edits.Get(zx, zz) ?? new ZoneEdit(zx, zz);
					for (int i = 0; i < EditStore.Cells; i++)
					{
						e.Lift[i] += f(zx * 64 - 32 + i % 65, zz * 64 - 32 + i / 65);
					}
					Edits.Put(e);
				}
			}
		}

		public Uplift.Plan Save()
		{
			var plan = Uplift.Make(World, Terrain, Edits, NextId)!;
			Uplift.Apply(plan, Edits);
			var r = WorldWriter.Save(World, Edits.All().Where(e => e.Changed).ToList(), Edits.Deleted, Edits.Added, Edits.Resets);
			Assert.True(r.Saved, r.Message);
			Assert.True(r.Skipped.Count == 0, string.Join("; ", r.Skipped.Take(3)));
			return plan;
		}
	}

	// A smooth hill (or hollow) of the given height and radius.
	private static Func<float, float, float> Bell(float cx, float cz, float height, float radius) => (x, z) =>
	{
		float d = MathF.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / radius;
		return d >= 1 ? 0 : height * (1 - d * d) * (1 - d * d);
	};

	// The ground asked for and the ground the saved world makes, over a square around (cx, cz).
	private static List<float> Misses(Func<float, float, float> want, Open after, float cx, float cz, int half)
	{
		var misses = new List<float>();
		for (int z = (int)cz - half; z <= (int)cz + half; z += 2)
		{
			for (int x = (int)cx - half; x <= (int)cx + half; x += 2)
			{
				misses.Add(MathF.Abs(want(x, z) - after.Height(x, z)));
			}
		}
		misses.Sort();
		return misses;
	}

	[Fact]
	public void AHillPastTheLimitIsSavedAsGroundDiscsAndComesOutAsAsked()
	{
		using var w = new TempWorld();
		var o = Open.Of(w.Load());
		// Within the test world's saved zones (0 to 15 both ways), over the edge of its trees.
		const float cx = 40, cz = 30;
		var hill = Bell(cx, cz, 30, 60);
		var tree = o.World.Objects.Where(t => ObjectKinds.Of(PrefabCatalog.NameOf(t.Prefab), false) == ObjectKind.Trees)
			.OrderBy(t => Vector2.Distance(new(t.Position.X, t.Position.Z), new(cx, cz))).First();
		Assert.True(hill(tree.Position.X, tree.Position.Z) > Uplift.ClearHeight);
		var before = new Dictionary<(int, int), float>();
		for (int z = (int)cz - 70; z <= (int)cz + 70; z += 2)
		{
			for (int x = (int)cx - 70; x <= (int)cx + 70; x += 2)
			{
				before[(x, z)] = o.Height(x, z);
			}
		}
		var piecesBefore = o.World.Pieces.Count;
		o.Lift(cx, cz, hill);
		var plan = o.Save();
		Assert.True(plan.Discs > 20, plan.Describe());
		Assert.Equal(0, plan.OldDiscs);

		var after = Open.Of(w.Load());
		Assert.Equal(plan.Discs, after.World.Discs.Count);
		// No lift is left to save: it is all ground discs and edits now.
		Assert.DoesNotContain(after.Edits.All(), e => e.HasLift);
		var misses = Misses((x, z) => before[((int)x, (int)z)] + hill(x, z), after, cx, cz, 70);
		Assert.True(misses[misses.Count * 99 / 100] < 0.5f, $"99% within 0.5 m: {misses[misses.Count * 99 / 100]:0.00}");
		Assert.True(misses[^1] < 4f, $"worst {misses[^1]:0.00} m");
		// The top really is beyond the edits' reach: the ground discs carry it.
		Assert.True(after.Height(cx, cz) - before[((int)cx, (int)cz)] > 25);
		// The tree under the hill is taken away; building pieces are not.
		Assert.DoesNotContain(after.World.Objects, t => t.Prefab == tree.Prefab && Vector3.Distance(t.Position, tree.Position) < 0.01f);
		Assert.Equal(piecesBefore, after.World.Pieces.Count);
		// Invisible: not an object of the View, and not outlined as a location's flattening.
		Assert.DoesNotContain(after.World.Objects, t => t.Prefab == WorldSave.LocationProxyPrefab);
		Assert.All(new TerrainModifiers(after.World).InZone((int)MathF.Floor((cx + 32) / 64), (int)MathF.Floor((cz + 32) / 64)).Where(m => m.LevelRadius == Uplift.DiscRadius), m => Assert.True(m.Disc));
	}

	[Fact]
	public void LiftingAgainReplacesTheDiscsAndLoweringItAllBackTakesThemAway()
	{
		using var w = new TempWorld();
		var o = Open.Of(w.Load());
		const float cx = 300, cz = 300;
		float start = o.Height(cx, cz);
		o.Lift(cx, cz, Bell(cx, cz, 25, 50));
		o.Save();

		// Higher: the old discs are worked out again with new ones.
		var again = Open.Of(w.Load());
		int discs = again.World.Discs.Count;
		again.Lift(cx, cz, Bell(cx, cz, 15, 50));
		var plan = again.Save();
		Assert.True(plan.OldDiscs > 0);
		var third = Open.Of(w.Load());
		Assert.InRange(third.Height(cx, cz) - start, 38, 42);
		Assert.True(third.World.Discs.Count >= discs / 2);

		// All the way back down: no ground disc is needed any more.
		third.Lift(cx, cz, Bell(cx, cz, -40, 50));
		third.Save();
		var back = Open.Of(w.Load());
		Assert.Empty(back.World.Discs);
		Assert.InRange(back.Height(cx, cz) - start, -1, 1);
	}

	[Fact]
	public void TheGroundCanGoDownPastTheLimitToo()
	{
		using var w = new TempWorld();
		var o = Open.Of(w.Load());
		const float cx = 400, cz = 200;
		float start = o.Height(cx, cz);
		o.Lift(cx, cz, Bell(cx, cz, -25, 50));
		var plan = o.Save();
		Assert.True(plan.Discs > 0);
		var after = Open.Of(w.Load());
		Assert.InRange(after.Height(cx, cz) - start, -26, -24);
	}

	[Fact]
	public void NoLimitPutsWhatIsPastTheLimitInTheLift()
	{
		var g = new TerrainEditor.Desktop.Ground(65, 65, 0, 0, 1);
		int p = 32 * 65 + 32;
		g.Base[p] = 30;
		g.NoLimit = true;
		Assert.False(g.SetHeight(p, 60));
		Assert.Equal(60, g.HeightOf(p), 0.001f);
		Assert.Equal(30, g.Lift[p], 0.001f);
		Assert.False(g.AtLimit(p));
		// With the limit back, the ±8 m count from the lifted ground.
		g.NoLimit = false;
		Assert.True(g.SetHeight(p, 80));
		Assert.Equal(68, g.HeightOf(p), 0.001f);
		Assert.True(g.AtLimit(p));
		var e = g.ZoneEdit(0, 0);
		Assert.Equal(30, e.Lift[p], 0.001f);
		Assert.True(e.HasLift);
	}

	[Fact]
	public void WithoutLiftThereIsNothingToDo()
	{
		using var w = new TempWorld();
		var o = Open.Of(w.Load());
		Assert.Null(Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId));
	}

	[Fact]
	public void ADiscIsALocationProxyTheGameSpawnsAsDevGround1()
	{
		var z = ZdoData.Parse(Uplift.DiscBytes(new Vector3(10, 55, -20)));
		Assert.Equal(WorldSave.LocationProxyPrefab, z.Prefab);
		Assert.Equal(55, z.Position.Y, 0.001f);
		Assert.Contains(z.IntList, i => i.Key == StableHash.Of("location") && i.Value == StableHash.Of("DevGround1"));
		Assert.Contains(z.IntList, i => i.Key == StableHash.Of("seed"));
		// Persistent (kept in the save), a solid object like the game's own proxies.
		Assert.Equal(0x100 | (2 << 10), z.BaseFlags & 0xFF00 & ~0x1000 & ~0x2000);
	}
}
