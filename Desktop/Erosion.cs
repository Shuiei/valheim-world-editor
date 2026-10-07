namespace TerrainEditor.Desktop;

// Erosion, like the web editor's (editor/erosion.js; VoxelSniper's erode brush, Axiom's erosion):
// ground that weathers like real ground. Thermal: where the ground is steeper than its resting angle,
// material slides down to its lower neighbours (screes, softened cliffs). Water: drops of rain run
// downhill, dig where they speed up and leave what they carry where they slow down (gullies, fans,
// smooth valleys). Both work on a local copy of the heights, written back through SetHeight (so the
// ±8 m limit applies).
public static class Erosion
{
	private static readonly (int Dx, int Dz)[] N8 = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

	// h: heights of a w × d window (row by row), wt: how much each point may change (0..1). talus:
	// steepest slope (rise per metre) that stays put. amount: share of the excess moved per pass.
	public static void Thermal(float[] h, float[] wt, int w, int d, float talus, float amount, int passes)
	{
		var delta = new float[h.Length];
		var ds = new List<(int J, float Diff)>(8);
		for (int p = 0; p < passes; p++)
		{
			Array.Clear(delta);
			for (int z = 1; z < d - 1; z++)
			{
				for (int x = 1; x < w - 1; x++)
				{
					int i = z * w + x;
					float k = wt[i];
					if (k <= 0)
					{
						continue;
					}
					float total = 0, maxD = 0;
					ds.Clear();
					foreach (var (dx, dz) in N8)
					{
						int j = (z + dz) * w + x + dx;
						float dist = dx != 0 && dz != 0 ? MathF.Sqrt(2) : 1, diff = h[i] - h[j] - talus * dist;
						if (diff > 0 && wt[j] > 0)
						{
							ds.Add((j, diff));
							total += diff;
							maxD = MathF.Max(maxD, diff);
						}
					}
					if (total == 0)
					{
						continue;
					}
					// Move half of the largest excess (so the slope settles instead of swinging), shared out.
					float move = maxD * 0.5f * amount * k;
					delta[i] -= move;
					foreach (var (j, diff) in ds)
					{
						delta[j] += move * diff / total;
					}
				}
			}
			for (int i = 0; i < h.Length; i++)
			{
				h[i] += delta[i];
			}
		}
	}

	// Rain drops (after Hans Theobald Beyer's "Implementation of a method for hydraulic erosion").
	public static void Hydraulic(float[] h, float[] wt, int w, int d, int drops, float strength, Random rand)
	{
		const float inertia = 0.05f, capacityK = 4, deposit = 0.3f, evaporate = 0.02f, gravity = 4, minSlope = 0.01f;
		const int life = 40;
		float erode = 0.3f * strength;
		float At(int x, int z) => h[z * w + x];
		(float Gx, float Gz, float H) Gradient(float px, float pz)
		{
			int x = (int)MathF.Floor(px), z = (int)MathF.Floor(pz);
			float u = px - x, v = pz - z;
			float a = At(x, z), b = At(x + 1, z), c = At(x, z + 1), e = At(x + 1, z + 1);
			return ((b - a) * (1 - v) + (e - c) * v, (c - a) * (1 - u) + (e - b) * u, a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + e * u * v);
		}
		void Change(float px, float pz, float amount)
		{
			int x = (int)MathF.Floor(px), z = (int)MathF.Floor(pz);
			float u = px - x, v = pz - z;
			foreach (var (dx, dz, f) in new[] { (0, 0, (1 - u) * (1 - v)), (1, 0, u * (1 - v)), (0, 1, (1 - u) * v), (1, 1, u * v) })
			{
				int i = (z + dz) * w + x + dx;
				h[i] += amount * f * wt[i];
			}
		}
		for (int n = 0; n < drops; n++)
		{
			float px = 1 + (float)rand.NextDouble() * (w - 3), pz = 1 + (float)rand.NextDouble() * (d - 3);
			if (wt[(int)MathF.Floor(pz) * w + (int)MathF.Floor(px)] <= 0)
			{
				continue;
			}
			float dirX = 0, dirZ = 0, speed = 1, water = 1, sediment = 0;
			for (int step = 0; step < life; step++)
			{
				var g = Gradient(px, pz);
				dirX = dirX * inertia - g.Gx * (1 - inertia);
				dirZ = dirZ * inertia - g.Gz * (1 - inertia);
				float len = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
				if (len < 1e-6f)
				{
					break;
				}
				dirX /= len;
				dirZ /= len;
				float nx = px + dirX, nz = pz + dirZ;
				if (nx < 1 || nz < 1 || nx >= w - 2 || nz >= d - 2)
				{
					break;
				}
				float dh = Gradient(nx, nz).H - g.H;
				float capacity = MathF.Max(-dh, minSlope) * speed * water * capacityK;
				if (sediment > capacity || dh > 0)
				{
					// Uphill or full: drop sediment (enough to fill a pit going uphill).
					float amount = dh > 0 ? MathF.Min(dh, sediment) : (sediment - capacity) * deposit;
					sediment -= amount;
					Change(px, pz, amount);
				}
				else
				{
					float amount = MathF.Min((capacity - sediment) * erode, -dh);
					sediment += amount;
					Change(px, pz, -amount);
				}
				speed = MathF.Sqrt(MathF.Max(0, speed * speed + dh * gravity));
				water *= 1 - evaporate;
				px = nx;
				pz = nz;
			}
		}
	}

	// Copies the heights of a window of the ground, runs fn on them, and writes back what changed.
	public static (int X0, int Z0, int X1, int Z1) Run(Ground g, int x0, int z0, int x1, int z1, Func<int, int, int, float> weightOf,
		Action<float[], float[], int, int> fn, ICollection<int> touched, ref bool clamped)
	{
		x0 = Math.Max(0, x0);
		z0 = Math.Max(0, z0);
		x1 = Math.Min(g.W - 1, x1);
		z1 = Math.Min(g.H - 1, z1);
		int w = x1 - x0 + 1, d = z1 - z0 + 1;
		if (w < 3 || d < 3)
		{
			return (x0, z0, x1, z1);
		}
		var h = new float[w * d];
		var wt = new float[w * d];
		for (int z = 0; z < d; z++)
		{
			for (int x = 0; x < w; x++)
			{
				int gx = x0 + x, gz = z0 + z, p = gz * g.W + gx;
				h[z * w + x] = g.HeightOf(p);
				wt[z * w + x] = g.Locked(gx, gz) ? 0 : weightOf(gx, gz, p);
			}
		}
		var before = (float[])h.Clone();
		fn(h, wt, w, d);
		for (int i = 0; i < h.Length; i++)
		{
			if (MathF.Abs(h[i] - before[i]) < 1e-5f)
			{
				continue;
			}
			int p = (z0 + i / w) * g.W + x0 + i % w;
			clamped |= g.SetHeight(p, h[i]);
			touched.Add(p);
		}
		return (x0, z0, x1, z1);
	}

	public static float Talus(float degrees) => MathF.Tan(degrees * MathF.PI / 180);

	// The Area tool's Erode: both kinds over the whole selection, in one go.
	public static (int X0, int Z0, int X1, int Z1) Area(Ground g, AreaTool.Weights a, float restAngle, Random rand, ICollection<int> touched)
	{
		var weights = a.Cells.ToDictionary(c => c.G, c => c.W);
		bool clamped = false;
		float talus = Talus(restAngle);
		return Run(g, a.X0 - 1, a.Z0 - 1, a.X1 + 1, a.Z1 + 1, (_, _, p) => weights.GetValueOrDefault(p), (h, wt, w, d) =>
		{
			Thermal(h, wt, w, d, talus, 0.8f, 12);
			Hydraulic(h, wt, w, d, (int)MathF.Ceiling(w * d * 0.6f), 1, rand);
			Thermal(h, wt, w, d, talus, 0.5f, 4);
		}, touched, ref clamped);
	}
}
