namespace TerrainEditor.Desktop;

// The Mask, like the web editor's (editor/masks.js; WorldEdit's masks, WorldPainter's filters): limits
// brushes, paths, shapes, area actions and pastes to some biomes, a height range, a slope range or
// painted / unpainted ground. Off, everything passes.
public sealed class Mask
{
	public static readonly (int Biome, string Name)[] BiomeNames =
	{
		(1, "Meadows"), (8, "Black Forest"), (2, "Swamp"), (4, "Mountain"), (16, "Plains"),
		(512, "Mistlands"), (32, "Ashlands"), (64, "Deep North"), (256, "Ocean"),
	};

	public enum PaintRule { Any, Unpainted, Painted, Dirt, Cultivated, Paved }

	public bool On { get; set; }
	// None chosen: every biome.
	public HashSet<int> Biomes { get; } = new();
	public float? HeightMin { get; set; }
	public float? HeightMax { get; set; }
	public float? SlopeMin { get; set; }
	public float? SlopeMax { get; set; }
	public PaintRule Paint { get; set; }
	public event Action? Changed;
	public void Notify() => Changed?.Invoke();

	// Settings that let (almost) nothing through.
	public string Warning()
	{
		var w = new List<string>();
		if (SlopeMax is <= 0)
		{
			w.Add($"Slope max {SlopeMax}° only lets perfectly flat ground through: empty the box for no limit.");
		}
		if (SlopeMin is float a && SlopeMax is float b && a > b)
		{
			w.Add("Slope min is above max: nothing matches.");
		}
		if (HeightMin is float c && HeightMax is float d && c > d)
		{
			w.Add("Height min is above max: nothing matches.");
		}
		return string.Join(" ", w);
	}

	// The mask over a ground (1 where it passes, 0 elsewhere), or null when off. biomes: each point's
	// biome. The ground is judged as it is now, when the mask is made: an edit that changes it while
	// asking (a mountain, a shape, a paste, a brush stroke) is not judged against its own changes (a
	// slope then mixed changed and unchanged neighbours). frozen: each point is also judged only once.
	public Func<int, float>? For(Ground g, int[] biomes, bool frozen = false)
	{
		if (!On)
		{
			return null;
		}
		var set = Biomes.Count > 0 ? new HashSet<int>(Biomes) : null;
		float? hmin = HeightMin, hmax = HeightMax, smin = SlopeMin, smax = SlopeMax;
		var paint = Paint;
		int w = g.W, h = g.H;
		float[]? heights = null;
		if (hmin != null || hmax != null || smin != null || smax != null)
		{
			heights = new float[w * h];
			for (int p = 0; p < heights.Length; p++)
			{
				heights[p] = g.HeightOf(p);
			}
		}
		byte[]? pmod = paint != PaintRule.Any ? (byte[])g.PMod.Clone() : null;
		float[]? colours = paint != PaintRule.Any ? (float[])g.Paint.Clone() : null;
		float Slope(int p)
		{
			int gx = p % w, gz = p / w;
			float hx = heights![gz * w + Math.Min(w - 1, gx + 1)] - heights[gz * w + Math.Max(0, gx - 1)];
			float hz = heights[Math.Min(h - 1, gz + 1) * w + gx] - heights[Math.Max(0, gz - 1) * w + gx];
			return MathF.Atan(MathF.Sqrt(hx * hx + hz * hz) / 2) * 180 / MathF.PI;
		}
		bool PaintOk(int p)
		{
			if (paint == PaintRule.Any)
			{
				return true;
			}
			bool painted = pmod![p] != 0;
			float r = colours![p * 4], gr = colours[p * 4 + 1], b = colours[p * 4 + 2];
			return paint switch
			{
				PaintRule.Unpainted => !painted || r + gr + b < 0.3f,
				_ when !painted => false,
				PaintRule.Painted => r + gr + b >= 0.3f,
				PaintRule.Dirt => r >= 0.5f,
				PaintRule.Cultivated => gr >= 0.5f,
				_ => b >= 0.5f,
			};
		}
		float Test(int p)
		{
			if (set != null && (p >= biomes.Length || !set.Contains(biomes[p])))
			{
				return 0;
			}
			if (hmin != null || hmax != null)
			{
				float at = heights![p];
				if (at < hmin || at > hmax)
				{
					return 0;
				}
			}
			if (smin != null || smax != null)
			{
				float s = Slope(p);
				if (s < smin || s > smax)
				{
					return 0;
				}
			}
			return PaintOk(p) ? 1 : 0;
		}
		if (!frozen)
		{
			return Test;
		}
		var seen = new Dictionary<int, float>();
		return p => seen.TryGetValue(p, out float v) ? v : seen[p] = Test(p);
	}

	// Alt + Shift + click: the height range around the clicked ground (±2 m).
	public void PickHeight(float h)
	{
		On = true;
		HeightMin = MathF.Round(h - 2, 1);
		HeightMax = MathF.Round(h + 2, 1);
		Notify();
	}
}
