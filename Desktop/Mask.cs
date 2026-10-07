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
	// biome. frozen: judge each point once, as the ground was the first time it was asked (a brush
	// stroke is not stopped half-way by its own changes).
	public Func<int, float>? For(Ground g, int[] biomes, bool frozen = false)
	{
		if (!On)
		{
			return null;
		}
		var set = Biomes.Count > 0 ? new HashSet<int>(Biomes) : null;
		float? hmin = HeightMin, hmax = HeightMax, smin = SlopeMin, smax = SlopeMax;
		var paint = Paint;
		float Slope(int p)
		{
			int gx = p % g.W, gz = p / g.W;
			float hx = g.HeightOf(gz * g.W + Math.Min(g.W - 1, gx + 1)) - g.HeightOf(gz * g.W + Math.Max(0, gx - 1));
			float hz = g.HeightOf(Math.Min(g.H - 1, gz + 1) * g.W + gx) - g.HeightOf(Math.Max(0, gz - 1) * g.W + gx);
			return MathF.Atan(MathF.Sqrt(hx * hx + hz * hz) / 2) * 180 / MathF.PI;
		}
		bool PaintOk(int p)
		{
			if (paint == PaintRule.Any)
			{
				return true;
			}
			bool painted = g.PMod[p] != 0;
			float r = g.Paint[p * 4], gr = g.Paint[p * 4 + 1], b = g.Paint[p * 4 + 2];
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
				float h = g.HeightOf(p);
				if (h < hmin || h > hmax)
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
