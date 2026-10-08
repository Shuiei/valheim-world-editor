using TerrainEditor.Editing;
using TerrainService = ValheimGen.TerrainService;

namespace TerrainEditor.Desktop;

// The ground being edited, over the whole block of zones, the way the game keeps it (TerrainComp):
// per grid point the generated height, the level and smoothing changes, whether the point was
// changed, and the paint (dirt, cultivated, paved, vegetation) with whether it was painted. The same
// arrays as the web editor's page (editor.html), turned back into zones for the EditStore (uploadNow).
public sealed class Ground
{
	public int W { get; }
	public int H { get; }
	// The block's first zone and its size in zones.
	public int X0 { get; }
	public int Z0 { get; }
	public int Size { get; }
	public float[] Base { get; }
	public float[] Level { get; }
	public float[] Smooth { get; }
	public float[] Paint { get; }
	public float[] BaseMask { get; }
	public byte[] Mod { get; }
	public byte[] PMod { get; }

	// The values as loaded (or last saved), and each zone's own saved values: zones share their edge
	// points, and saved zones do not always agree on them. A point unchanged since loading is given
	// back with the zone's own value, so an untouched zone stays identical to what is saved.
	private State _loaded;
	private readonly Dictionary<(int, int), ZoneEdit?> _own = new();

	public Ground(int w, int h, int x0, int z0, int size)
	{
		W = w;
		H = h;
		X0 = x0;
		Z0 = z0;
		Size = size;
		int n = w * h;
		Base = new float[n];
		Level = new float[n];
		Smooth = new float[n];
		Mod = new byte[n];
		Paint = new float[n * 4];
		PMod = new byte[n];
		BaseMask = new float[n * 4];
		_loaded = Snapshot();
	}

	public static Ground Read(TerrainService terrain, EditStore edits, int x0, int z0, int size)
	{
		int w = size * 64 + 1;
		var g = new Ground(w, w, x0, z0, size);
		for (int zz = 0; zz < size; zz++)
		{
			for (int zx = 0; zx < size; zx++)
			{
				float[] b = terrain.BaseZone(x0 + zx, z0 + zz);
				float[] m = terrain.BaseMask(x0 + zx, z0 + zz);
				for (int k = 0; k < EditStore.Grid; k++)
				{
					for (int l = 0; l < EditStore.Grid; l++)
					{
						int i = k * EditStore.Grid + l, p = (zz * 64 + k) * w + zx * 64 + l;
						g.Base[p] = b[i];
						for (int c = 0; c < 4; c++)
						{
							g.BaseMask[p * 4 + c] = m[i * 4 + c];
						}
					}
				}
			}
		}
		g.TakeEdits(edits);
		return g;
	}

	// Starts over from the edits (when loaded, and after saving).
	public void TakeEdits(EditStore edits)
	{
		_own.Clear();
		for (int zz = 0; zz < Size; zz++)
		{
			for (int zx = 0; zx < Size; zx++)
			{
				var e = edits.Get(X0 + zx, Z0 + zz);
				_own[(zx, zz)] = e;
				for (int k = 0; k < EditStore.Grid; k++)
				{
					for (int l = 0; l < EditStore.Grid; l++)
					{
						int i = k * EditStore.Grid + l, p = (zz * 64 + k) * W + zx * 64 + l;
						Mod[p] = (byte)(e != null && e.Modified[i] ? 1 : 0);
						Level[p] = e?.Level[i] ?? 0;
						Smooth[p] = e?.Smooth[i] ?? 0;
						PMod[p] = (byte)(e != null && e.PaintModified[i] ? 1 : 0);
						for (int c = 0; c < 4; c++)
						{
							Paint[p * 4 + c] = e?.Paint[i * 4 + c] ?? 0;
						}
					}
				}
			}
		}
		_loaded = Snapshot();
	}

	// Same as TerrainComp.ApplyToHeightmap: the edited height never leaves the original ground ± 8 m.
	public float HeightOf(int g) => Mod[g] != 0 ? Math.Clamp(Base[g] + Level[g] + Smooth[g], Base[g] - EditStore.MaxLevel, Base[g] + EditStore.MaxLevel) : Base[g];

	public bool AtLimit(int g) => Mod[g] != 0 && MathF.Abs(Level[g] + Smooth[g]) >= EditStore.MaxLevel - 0.05f;

	// The outer line of points stays as it is, so the block always joins its neighbours.
	public bool Locked(int gx, int gz) => gx == 0 || gz == 0 || gx == W - 1 || gz == H - 1;

	// Moves a point to a height (kept within the game's limit); true when the limit stopped it.
	public bool SetHeight(int g, float h)
	{
		float target = Math.Clamp(h, Base[g] - EditStore.MaxLevel, Base[g] + EditStore.MaxLevel);
		if (Mod[g] == 0)
		{
			Smooth[g] = 0;
			Mod[g] = 1;
		}
		// Keep the smoothing part and put the rest in the level part (itself limited to ± 8 m).
		Level[g] = Math.Clamp(target - Base[g] - Smooth[g], -EditStore.MaxLevel, EditStore.MaxLevel);
		return MathF.Abs(target - h) > 1e-4f;
	}

	// The paint mask the game's terrain shader reads: the paint where painted, the generated one otherwise.
	public float MaskOf(int g, int c) => PMod[g] != 0 ? Paint[g * 4 + c] : BaseMask[g * 4 + c];

	public sealed record State(float[] Level, float[] Smooth, byte[] Mod, float[] Paint, byte[] PMod);

	public State Snapshot() => new((float[])Level.Clone(), (float[])Smooth.Clone(), (byte[])Mod.Clone(), (float[])Paint.Clone(), (byte[])PMod.Clone());

	// Every zone (block-relative) holding one of the points (edge points belong to two or four zones).
	public List<(int X, int Z)> ZonesOf(IEnumerable<int> points)
	{
		var set = new HashSet<(int, int)>();
		foreach (int g in points)
		{
			int gx = g % W, gz = g / W;
			foreach (int zx in new[] { gx / 64, (int)Math.Floor((gx - 1) / 64.0) })
			{
				foreach (int zz in new[] { gz / 64, (int)Math.Floor((gz - 1) / 64.0) })
				{
					if (zx >= 0 && zx < Size && zz >= 0 && zz < Size)
					{
						set.Add((zx, zz));
					}
				}
			}
		}
		return set.OrderBy(z => z.Item2).ThenBy(z => z.Item1).ToList();
	}

	private bool UnchangedSinceLoad(int g)
	{
		if (Level[g] != _loaded.Level[g] || Smooth[g] != _loaded.Smooth[g] || Mod[g] != _loaded.Mod[g] || PMod[g] != _loaded.PMod[g])
		{
			return false;
		}
		for (int c = 0; c < 4; c++)
		{
			if (Paint[g * 4 + c] != _loaded.Paint[g * 4 + c])
			{
				return false;
			}
		}
		return true;
	}

	// A zone (block-relative) as the EditStore keeps it, in world zone coordinates.
	public ZoneEdit ZoneEdit(int zx, int zz)
	{
		var e = new ZoneEdit(X0 + zx, Z0 + zz);
		var own = _own.GetValueOrDefault((zx, zz));
		for (int k = 0; k < EditStore.Grid; k++)
		{
			for (int l = 0; l < EditStore.Grid; l++)
			{
				int g = (zz * 64 + k) * W + zx * 64 + l, i = k * EditStore.Grid + l;
				if (own != null && UnchangedSinceLoad(g))
				{
					e.Modified[i] = own.Modified[i];
					e.Level[i] = own.Level[i];
					e.Smooth[i] = own.Smooth[i];
					e.PaintModified[i] = own.PaintModified[i];
					Array.Copy(own.Paint, i * 4, e.Paint, i * 4, 4);
					continue;
				}
				e.Modified[i] = Mod[g] != 0;
				e.Level[i] = Level[g];
				e.Smooth[i] = Smooth[g];
				e.PaintModified[i] = PMod[g] != 0;
				Array.Copy(Paint, g * 4, e.Paint, i * 4, 4);
			}
		}
		return e;
	}
}
