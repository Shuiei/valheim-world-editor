using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Editing;

// No limit: ground beyond the game's ±8 m. The game clamps its saved ground edits (TerrainComp) to
// the original ground ± 8 m, but not the flattening of locations: every player's game (console too)
// levels a location's ground before it takes the original ground. So the editor places invisible
// locations of the game's own (DevGround1: no model, it only levels a disc of 8 m radius to its
// height and blends 5 m around it) as location proxies, and the game counts their ground as
// generated. A shape sculpted past the limit (ZoneEdit.Lift) becomes, when saving or applying:
// discs on a grid whose heights are fitted by working out the ground exactly as the game does
// (TerrainModifiers), the rest as ordinary ground edits (within ±8 m of the new ground), the discs'
// dirt painted over with the biome's own ground, and the trees, rocks and bushes where the ground
// moved far taken away. The discs are worked out again wherever the ground is lifted again.
public static class Uplift
{
	public const string DiscName = "DevGround1";

	public static readonly int Disc = StableHash.Of(DiscName);

	public static bool IsDisc(int location) => location == Disc;

	// DevGround1's TerrainModifier (terrain-modifiers.json): level radius, smoothing radius, paint radius.
	public const float DiscRadius = 8f, DiscReach = 12.96f, PaintRadius = 9.19f;

	// The modifier's place in the location (it decides the order the game applies discs in).
	private static readonly Vector3 DiscOffset = new(-0.0006f, 0f, 0.1006f);

	// Grid of disc middles (m), within reach of the ground they make.
	public const int Spacing = 6;

	// A point further than this from the ground without discs needs one (the edits make up the rest).
	private const float Need = 5f;

	// How many times the disc heights are fitted against the ground the game would make, the passes
	// over the discs each time, and how much a disc resists moving (for discs with few points).
	private const int Rounds = 8, Sweeps = 12;
	private const double Damping = 0.25;

	// Each fitting step moves a disc this share of the way (the game's order shifts as discs move).
	private const double Step = 0.8;

	// Natural objects further than this from the ground they stood on are taken away.
	public const float ClearHeight = 2f;

	private static readonly int LocationKey = StableHash.Of("location"), SeedKey = StableHash.Of("seed");

	// What a save or an apply does to the edits: the zones' new ground, objects to take away (old
	// discs, natural objects), and the new discs. Worst: the largest difference left between the
	// ground asked for and what the game will make (beyond the edits' ±8 m).
	public sealed record Plan(List<ZoneEdit> Zones, List<int> Delete, List<NewObject> Add, int Discs, int OldDiscs, int Cleared, float Worst)
	{
		// Lifted zones the game has not generated yet: their lift was dropped.
		public int Ungenerated { get; init; }

		public string Describe() => $"{Discs} ground disc(s){(OldDiscs > 0 ? $" ({OldDiscs} replaced)" : "")}"
			+ (Cleared > 0 ? $", {Cleared} tree(s)/rock(s) taken away" : "")
			+ (Worst > EditStore.MaxLevel + 0.5f ? $"; some ground is up to {Worst - EditStore.MaxLevel:0.#} m off (too steep)" : "")
			+ (Ungenerated > 0 ? $"; {Ungenerated} zone(s) not generated yet were left as they were (visit them in game first)" : "");
	}

	// The editor's discs as they are now: the world's (not deleted) and the new ones not saved yet.
	public static List<(int Id, Vector3 Position)> CurrentDiscs(WorldSave world, EditStore edits)
	{
		var deleted = edits.Deleted;
		var list = world.Discs.Where(d => !deleted.Contains(d.Id)).ToList();
		list.AddRange(edits.Added.Where(IsDiscObject).Select(n => (n.Id, n.Position)));
		return list;
	}

	private static bool IsDiscObject(NewObject n) =>
		n.Prefab == WorldSave.LocationProxyPrefab && n.Raw != null && ZdoData.Parse(n.Raw).IntList.Any(i => i.Key == LocationKey && i.Value == Disc);

	// The world's placed objects with modifiers as the game will have them: the world's discs replaced
	// by the current ones (and, for a plan, without the ones it replaces).
	public static List<PlacedObject> PlacedNow(WorldSave world, EditStore edits, IEnumerable<(int Id, Vector3 Position)>? without = null)
	{
		var discs = CurrentDiscs(world, edits);
		if (without != null)
		{
			var drop = without.Select(d => d.Id).ToHashSet();
			discs = discs.Where(d => !drop.Contains(d.Id)).ToList();
		}
		var placed = world.Placed.Where(p => !IsDisc(p.Location)).ToList();
		placed.AddRange(discs.Select(d => DiscAt(d.Position)));
		return placed;
	}

	private static PlacedObject DiscAt(Vector3 p) => new(WorldSave.LocationProxyPrefab, Disc, p, Vector3.Zero, 0);

	// The disc as a saved object: a location proxy (persistent, solid, like the game's own).
	public static byte[] DiscBytes(Vector3 position)
	{
		var z = ZdoData.Parse(ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, 0x100 | (2 << 10), position, Vector3.Zero, 0f));
		z.Set("ints", LocationKey, Disc.ToString(System.Globalization.CultureInfo.InvariantCulture));
		z.Set("ints", SeedKey, "0");
		return z.Serialize();
	}

	// A zone the save has room for (a chunk file: what WorldWriter needs); the running game takes any.
	public static bool Generated(WorldSave world, int zx, int zz) => world.IsLive || ChunkMath.Find(world.Chunks, zx, zz) != null;

	// The plan for every lift waiting in the edits, or null when there is none.
	public static Plan? Make(WorldSave world, ValheimGen.TerrainService terrain, EditStore edits, Func<int> nextId)
	{
		var lifted = edits.All().Where(e => e.HasLift).ToList();
		if (lifted.Count == 0)
		{
			return null;
		}
		// The block of zones worked on: the lifted ones and those the discs reach from them.
		int reachZones = (int)MathF.Ceiling((Spacing + DiscRadius + DiscReach + 8f) / 64f);
		int zx0 = lifted.Min(e => e.ZoneX) - reachZones, zx1 = lifted.Max(e => e.ZoneX) + reachZones;
		int zz0 = lifted.Min(e => e.ZoneZ) - reachZones, zz1 = lifted.Max(e => e.ZoneZ) + reachZones;
		var block = new Block(zx0, zz0, zx1, zz1);

		// The ground asked for, as it shows in the editor now (old discs, edits, lift), and the lift.
		float[] want = new float[block.N], lift = new float[block.N];
		var zones = new Dictionary<(int, int), ZoneEdit>();
		for (int zz = zz0; zz <= zz1; zz++)
		{
			for (int zx = zx0; zx <= zx1; zx++)
			{
				float[] b = terrain.BaseZone(zx, zz);
				ZoneEdit e = edits.Get(zx, zz) ?? new ZoneEdit(zx, zz);
				zones[(zx, zz)] = e;
				block.Each(zx, zz, (i, p) =>
				{
					want[p] = b[i] + (e.Modified[i] ? Math.Clamp(e.Level[i] + e.Smooth[i], -EditStore.MaxLevel, EditStore.MaxLevel) : 0) + e.Lift[i];
					lift[p] = e.Lift[i];
				});
			}
		}

		// Lift in zones not generated yet is dropped (the game would generate them on the old ground).
		var ungenerated = new HashSet<(int, int)>();
		foreach (var e in lifted.Where(e => !Generated(world, e.ZoneX, e.ZoneZ)))
		{
			ungenerated.Add((e.ZoneX, e.ZoneZ));
		}
		var open = new bool[block.N];
		foreach (var (zx, zz) in zones.Keys.Where(k => Generated(world, k.Item1, k.Item2)))
		{
			block.Each(zx, zz, (_, p) => open[p] = true);
		}
		foreach (var (zx, zz) in ungenerated)
		{
			// Points on the edge with a generated zone keep that zone's lift.
			block.Each(zx, zz, (_, p) =>
			{
				if (!open[p])
				{
					want[p] -= lift[p];
					lift[p] = 0;
				}
			});
		}

		// Old discs near the lift are worked out again with the new ones.
		var liftMask = Dilate(block, lift, l => l != 0, (int)DiscRadius + Spacing);
		var current = CurrentDiscs(world, edits);
		var old = current.Where(d => block.At(d.Position) is int p && liftMask[p]).ToList();
		var keep = PlacedNow(world, edits, old).Where(p => block.Near(p.Position, 100f)).ToList();

		var raw = new Dictionary<(int, int), float[]>();
		float[] Ground(List<Vector3> discs)
		{
			var mods = new TerrainModifiers(keep.Concat(discs.Select(DiscAt)));
			float[] g = new float[block.N];
			for (int zz = zz0; zz <= zz1; zz++)
			{
				for (int zx = zx0; zx <= zx1; zx++)
				{
					if (!raw.TryGetValue((zx, zz), out float[]? r))
					{
						r = raw[(zx, zz)] = terrain.RawZone(zx, zz);
					}
					float[] h = (float[])r.Clone();
					mods.Apply(zx, zz, h);
					block.Each(zx, zz, (i, p) => g[p] = h[i]);
				}
			}
			return g;
		}

		// Where the ground needs a disc: near the lift or the discs replaced (not where ordinary edits
		// are), further from the ground without discs than the edits reach well.
		var region = (bool[])liftMask.Clone();
		foreach (var d in old)
		{
			block.Disc(d.Position, DiscReach, p => region[p] = true);
		}
		float[] bare = Ground(new());
		var need = new bool[block.N];
		for (int p = 0; p < block.N; p++)
		{
			need[p] = region[p] && open[p] && MathF.Abs(want[p] - bare[p]) > Need;
		}
		var nodes = new Dictionary<(int, int), float>();
		void AddNodesNear(int p)
		{
			var (wx, wz) = block.World(p);
			int r = (int)DiscRadius;
			for (int nz = (int)Math.Floor((wz - r) / (double)Spacing); nz <= (int)Math.Ceiling((wz + r) / (double)Spacing); nz++)
			{
				for (int nx = (int)Math.Floor((wx - r) / (double)Spacing); nx <= (int)Math.Ceiling((wx + r) / (double)Spacing); nx++)
				{
					int cx = nx * Spacing, cz = nz * Spacing;
					if ((cx - wx) * (cx - wx) + (cz - wz) * (cz - wz) <= DiscRadius * DiscRadius && !nodes.ContainsKey((cx, cz)) && block.At(new Vector3(cx, 0, cz)) is int q && open[q])
					{
						nodes[(cx, cz)] = want[q];
					}
				}
			}
		}
		for (int p = 0; p < block.N; p++)
		{
			if (need[p])
			{
				AddNodesNear(p);
			}
		}

		List<Vector3> Discs() => nodes.Select(n => new Vector3(n.Key.Item1, n.Value, n.Key.Item2)).ToList();
		float[] ground = bare;
		// The game's order changes with the heights, which the fit does not see: each round's discs are
		// checked against the real ground, and the best kept.
		List<Vector3>? best = null;
		double bestMiss = double.MaxValue;
		double Miss(float[] g)
		{
			double m = 0;
			for (int p = 0; p < block.N; p++)
			{
				if (region[p] || lift[p] != 0)
				{
					// What the edits cannot make up counts most.
					float r = MathF.Abs(want[p] - g[p]);
					m += r * r + (r > EditStore.MaxLevel ? 100 * (r - EditStore.MaxLevel) * (r - EditStore.MaxLevel) : 0);
				}
			}
			return m;
		}
		for (int round = 0; round < Rounds && nodes.Count > 0; round++)
		{
			var discs = Discs();
			ground = Ground(discs);
			// The game's ground is a weighted mix of the disc heights (each disc levels its points to its
			// height, then blends the ring around it towards it), in the game's order: the disc heights
			// are fitted to it by least squares, a disc at a time.
			var columns = Weights(block, discs);
			// Each disc stays within the heights asked for around it: one mostly covered by others would
			// otherwise go far up or down for its few points, and its new height changes the game's order.
			var range = discs.Select(d =>
			{
				float lo = float.MaxValue, hi = float.MinValue;
				block.Disc(d, DiscRadius, p => { lo = MathF.Min(lo, want[p]); hi = MathF.Max(hi, want[p]); });
				return (lo, hi);
			}).ToList();
			var r = new float[block.N];
			for (int p = 0; p < block.N; p++)
			{
				r[p] = want[p] - ground[p];
			}
			for (int sweep = 0; sweep < Sweeps; sweep++)
			{
				for (int k = 0; k < discs.Count; k++)
				{
					double num = 0, den = Damping;
					foreach (var (p, w) in columns[k])
					{
						num += w * r[p];
						den += w * w;
					}
					float d = Math.Clamp((float)(Step * num / den), range[k].lo - discs[k].Y, range[k].hi - discs[k].Y);
					foreach (var (p, w) in columns[k])
					{
						r[p] -= w * d;
					}
					discs[k] += new Vector3(0, d, 0);
				}
			}
			foreach (var d in discs)
			{
				nodes[((int)d.X, (int)d.Z)] = d.Y;
			}
			ground = Ground(Discs());
			double miss = Miss(ground);
			if (miss < bestMiss)
			{
				bestMiss = miss;
				best = Discs();
			}
			if (round < Rounds - 1)
			{
				// Still too far off out of the discs' reach (at a disc's blended edge): one more disc there.
				int before = nodes.Count;
				for (int p = 0; p < block.N; p++)
				{
					if (region[p] && open[p] && MathF.Abs(want[p] - ground[p]) > Need)
					{
						AddNodesNear(p);
					}
				}
				if (nodes.Count == before && round >= 2)
				{
					break;
				}
			}
		}
		var final = best ?? Discs();
		ground = final.Count > 0 ? Ground(final) : bare;

		// The zones' new edits: what the discs leave, within ±8 m; the discs' dirt painted back.
		float worst = 0;
		var painted = new bool[block.N];
		foreach (var d in final)
		{
			block.Disc(d, PaintRadius + 1, p => painted[p] = true);
		}
		var result = new List<ZoneEdit>();
		foreach (var ((zx, zz), e) in zones)
		{
			if (!Generated(world, zx, zz))
			{
				// Nothing can be saved there: the lift is just dropped.
				if (e.HasLift)
				{
					var dropped = e.Clone();
					Array.Clear(dropped.Lift);
					result.Add(dropped);
				}
				continue;
			}
			float[] mask = terrain.BaseMask(zx, zz);
			var n = new ZoneEdit(zx, zz);
			bool any = false;
			block.Each(zx, zz, (i, p) =>
			{
				float r = want[p] - ground[p];
				if (lift[p] != 0 || region[p])
				{
					worst = MathF.Max(worst, MathF.Abs(r));
				}
				if (e.Modified[i] || MathF.Abs(r) > 0.01f)
				{
					n.Modified[i] = true;
					n.Level[i] = Math.Clamp(r, -EditStore.MaxLevel, EditStore.MaxLevel);
				}
				n.PaintModified[i] = e.PaintModified[i];
				Array.Copy(e.PaintModified[i] ? e.Paint : mask, i * 4, n.Paint, i * 4, 4);
				if (painted[p] && !e.PaintModified[i])
				{
					n.PaintModified[i] = true;
				}
				any |= n.Modified[i] != e.Modified[i] || n.Level[i] != e.Level[i] || n.PaintModified[i] != e.PaintModified[i] || e.Lift[i] != 0;
			});
			if (any || e.HasLift)
			{
				result.Add(n);
			}
		}

		// Natural objects where the lift moved the ground far.
		var deleted = edits.Deleted;
		var clear = world.Objects.Where(o => !deleted.Contains(o.Id) && block.At(o.Position) is int p && MathF.Abs(lift[p]) > ClearHeight
			&& ObjectKinds.Of(PrefabCatalog.NameOf(o.Prefab), false) is ObjectKind.Trees or ObjectKind.Rocks or ObjectKind.Ore or ObjectKind.Bushes or ObjectKind.Pickables)
			.Select(o => o.Id).ToList();
		var add = final.Select(d => new NewObject(nextId(), WorldSave.LocationProxyPrefab, d, Vector3.Zero, 0f, Fresh: false, Raw: DiscBytes(d))).ToList();
		return new Plan(result, old.Select(d => d.Id).Concat(clear).ToList(), add, final.Count, old.Count, clear.Count, worst) { Ungenerated = ungenerated.Count };
	}

	// Applies a plan to the edits (the zones, the objects taken away and added).
	public static void Apply(Plan plan, EditStore edits)
	{
		foreach (ZoneEdit z in plan.Zones)
		{
			edits.Put(z);
		}
		edits.SetDeleted(plan.Delete, true);
		edits.AddObjects(plan.Add);
	}

	// For each disc, the points it reaches and how much of its height each ends up with: the game's
	// Heightmap.LevelTerrain (the point takes the disc's height) then SmoothTerrain2 (the point moves a
	// share 1 - (d/r)^3 towards it), discs in TerrainModifier.SortByModifiers order.
	private static List<(int P, float W)>[] Weights(Block block, List<Vector3> discs)
	{
		var at = new Dictionary<int, List<(int K, float W)>>();
		foreach (int k in Enumerable.Range(0, discs.Count).OrderBy(k => (discs[k] + DiscOffset).LengthSquared()))
		{
			block.Disc(discs[k], DiscReach, (p, d) =>
			{
				if (d <= DiscRadius)
				{
					at[p] = new() { (k, 1f) };
					return;
				}
				float u = d / DiscReach, t = 1f - u * u * u;
				if (!at.TryGetValue(p, out var list))
				{
					at[p] = new() { (k, t) };
					return;
				}
				for (int i = 0; i < list.Count; i++)
				{
					list[i] = (list[i].K, list[i].W * (1 - t));
				}
				list.Add((k, t));
			});
		}
		var columns = new List<(int, float)>[discs.Count];
		for (int k = 0; k < discs.Count; k++)
		{
			columns[k] = new();
		}
		foreach (var (p, list) in at)
		{
			foreach (var (k, w) in list)
			{
				if (w > 1e-4f)
				{
					columns[k].Add((p, w));
				}
			}
		}
		return columns;
	}

	private static bool[] Dilate(Block block, float[] values, Func<float, bool> on, int r)
	{
		var src = new bool[block.N];
		for (int p = 0; p < block.N; p++)
		{
			src[p] = on(values[p]);
		}
		// Separable square dilation (rows, then columns): every point within r of one that is on.
		var rows = new bool[block.N];
		for (int z = 0; z < block.H; z++)
		{
			int last = int.MinValue / 2;
			for (int x = 0; x < block.W; x++)
			{
				if (src[z * block.W + x]) last = x;
				rows[z * block.W + x] = x - last <= r;
			}
			last = int.MaxValue / 2;
			for (int x = block.W - 1; x >= 0; x--)
			{
				if (src[z * block.W + x]) last = x;
				rows[z * block.W + x] |= last - x <= r;
			}
		}
		var res = new bool[block.N];
		for (int x = 0; x < block.W; x++)
		{
			int last = int.MinValue / 2;
			for (int z = 0; z < block.H; z++)
			{
				if (rows[z * block.W + x]) last = z;
				res[z * block.W + x] = z - last <= r;
			}
			last = int.MaxValue / 2;
			for (int z = block.H - 1; z >= 0; z--)
			{
				if (rows[z * block.W + x]) last = z;
				res[z * block.W + x] |= last - z <= r;
			}
		}
		return res;
	}

	// A block of zones as one grid of world points, one per metre (zones share their edge points).
	private sealed class Block(int zx0, int zz0, int zx1, int zz1)
	{
		public readonly int X0 = zx0 * 64 - 32, Z0 = zz0 * 64 - 32;
		public readonly int W = (zx1 - zx0 + 1) * 64 + 1, H = (zz1 - zz0 + 1) * 64 + 1;
		public int N => W * H;

		public (int X, int Z) World(int p) => (X0 + p % W, Z0 + p / W);

		public int? At(Vector3 v)
		{
			int x = (int)MathF.Round(v.X) - X0, z = (int)MathF.Round(v.Z) - Z0;
			return x >= 0 && z >= 0 && x < W && z < H ? z * W + x : null;
		}

		public bool Near(Vector3 v, float margin) => v.X >= X0 - margin && v.Z >= Z0 - margin && v.X <= X0 + W - 1 + margin && v.Z <= Z0 + H - 1 + margin;

		// A zone's 65x65 points: (index in the zone, point in the block).
		public void Each(int zx, int zz, Action<int, int> f)
		{
			int ox = zx * 64 - 32 - X0, oz = zz * 64 - 32 - Z0;
			for (int k = 0; k < 65; k++)
			{
				for (int l = 0; l < 65; l++)
				{
					f(k * 65 + l, (oz + k) * W + ox + l);
				}
			}
		}

		// The points within r of a disc's middle (as Heightmap.LevelTerrain counts them).
		public void Disc(Vector3 c, float r, Action<int> f) => Disc(c, r, (p, _) => f(p));

		public void Disc(Vector3 c, float r, Action<int, float> f)
		{
			int cx = (int)MathF.Floor(c.X + 0.5f) - X0, cz = (int)MathF.Floor(c.Z + 0.5f) - Z0, n = (int)MathF.Ceiling(r);
			for (int z = Math.Max(0, cz - n); z <= Math.Min(H - 1, cz + n); z++)
			{
				for (int x = Math.Max(0, cx - n); x <= Math.Min(W - 1, cx + n); x++)
				{
					float dx = x - cx, dz = z - cz;
					float d = MathF.Sqrt(dx * dx + dz * dz);
					if (d <= r)
					{
						f(z * W + x, d);
					}
				}
			}
		}
	}
}
