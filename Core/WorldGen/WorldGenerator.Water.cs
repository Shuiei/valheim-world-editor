using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

// Lakes, rivers and streams, laid out once when the world is made (each from its own seed, drawn in
// the game's order) and then read by the ground: lakes where the continents dip under the water,
// rivers between lakes (not through high ground), streams on the slopes. Each is drawn into a grid of
// 64 m cells as round points along a wavy line; the ground reads how deep in a river a point is.
public sealed partial class WorldGenerator
{
	// A river or stream: from one end to the other, its widths, and its waves.
	public sealed class River
	{
		public Vector2 P0 { get; set; }
		public Vector2 P1 { get; set; }
		public Vector2 Center { get; set; }
		public float WidthMin { get; set; }
		public float WidthMax { get; set; }
		public float CurveWidth { get; set; }
		public float CurveWavelength { get; set; }
	}

	// One point of a river's line: where, how wide (and that squared).
	private readonly struct RiverPoint(Vector2 at, float width)
	{
		public readonly Vector2 At = at;
		public readonly float Width = width;
		public readonly float WidthSquared = (float)((double)width * width);
	}

	private enum Streams
	{
		All,
		NotDeepNorth,
		DeepNorthOnly,
	}

	private readonly int _riverSeed, _streamSeed;
	private List<Vector2> _lakes = new();
	private List<River> _rivers = new();
	private List<River> _streams = new();
	// The river points touching each cell (read by many threads once the world is made).
	private readonly Dictionary<Vector2i, RiverPoint[]> _riverCells = new();

	public List<Vector2> GetLakes() => _lakes;

	public List<River> GetRivers() => _rivers;

	public List<River> GetStreams() => _streams;

	private void LayOutWater()
	{
		_lakes = FindLakes();
		_rivers = PlaceRivers();
		_streams = PlaceStreams(deepNorth: false);
		// The Deep North's streams are drawn into the ground too (the game does not keep their list).
		PlaceStreams(deepNorth: true);
	}

	// ---- Lakes: every 128 m where the continents are under 0.05, nearby ones merged.

	private List<Vector2> FindLakes()
	{
		var low = new List<Vector2>();
		for (float z = -worldSize; z <= worldSize; z = (float)((double)z + 128.0))
		{
			for (float x = -worldSize; x <= worldSize; x = (float)((double)x + 128.0))
			{
				if (!(GenMath.Length(x, z) > worldSize) && Continent(x, z) < 0.05f)
				{
					low.Add(new Vector2(x, z));
				}
			}
		}
		return Merge(low, 800f);
	}

	// Points within range of each other merged into one (halfway each time), in the game's order.
	private static List<Vector2> Merge(List<Vector2> points, float range)
	{
		var merged = new List<Vector2>();
		while (points.Count > 0)
		{
			var p = points[0];
			points.RemoveAt(0);
			for (int near; points.Count > 0 && (near = Nearest(points, p, range)) != -1;)
			{
				p = (p + points[near]) * 0.5f;
				points[near] = points[^1];
				points.RemoveAt(points.Count - 1);
			}
			merged.Add(p);
		}
		return merged;
	}

	// The nearest other point within range (the first of equals), or -1.
	private static int Nearest(List<Vector2> points, Vector2 p, float range)
	{
		int best = -1;
		float bestDistance = 99999f;
		for (int i = 0; i < points.Count; i++)
		{
			if (points[i] == p)
			{
				continue;
			}
			float d = Vector2.Distance(p, points[i]);
			if (d < range && d < bestDistance)
			{
				best = i;
				bestDistance = d;
			}
		}
		return best;
	}

	// ---- Rivers: from each lake to a random lake within 2 km (5 km for one with no river yet) whose
	// way stays low and leaves the water somewhere.

	private List<River> PlaceRivers()
	{
		var saved = Rnd.state;
		Rnd.InitState(_riverSeed);
		var rivers = new List<River>();
		var from = new List<Vector2>(_lakes);
		while (from.Count > 1)
		{
			var p = from[0];
			int to = RandomRiverEnd(rivers, p, 2000f);
			if (to == -1 && !rivers.Any(r => r.P0 == p || r.P1 == p))
			{
				to = RandomRiverEnd(rivers, p, 5000f);
			}
			if (to == -1)
			{
				from.RemoveAt(0);
				continue;
			}
			var river = new River { P0 = p, P1 = _lakes[to] };
			river.Center = (river.P0 + river.P1) * 0.5f;
			river.WidthMax = Rnd.Range(60f, 100f);
			river.WidthMin = Rnd.Range(60f, river.WidthMax);
			Curves(river);
			rivers.Add(river);
		}
		Draw(rivers, Streams.All);
		Rnd.state = saved;
		return rivers;
	}

	// A river's waves: a fifteenth of its length wide, a twentieth long.
	private static void Curves(River river)
	{
		float length = Vector2.Distance(river.P0, river.P1);
		river.CurveWidth = (float)((double)length / 15.0);
		river.CurveWavelength = (float)((double)length / 20.0);
	}

	private int RandomRiverEnd(List<River> rivers, Vector2 p, float range)
	{
		var ends = new List<int>();
		for (int i = 0; i < _lakes.Count; i++)
		{
			var q = _lakes[i];
			if (q != p && Vector2.Distance(p, q) < range && !rivers.Any(r => (r.P0 == p && r.P1 == q) || (r.P0 == q && r.P1 == p)) && RiverCanRun(p, q))
			{
				ends.Add(i);
			}
		}
		return ends.Count == 0 ? -1 : ends[Rnd.Range(0, ends.Count)];
	}

	// Along the way (every 128 m, ends left out): never over 0.4, and out of the water (over 0.05) at
	// least once.
	private bool RiverCanRun(Vector2 a, Vector2 b)
	{
		const float step = 128f;
		float length = Vector2.Distance(a, b);
		var dir = (b - a).normalized;
		bool allWater = true;
		for (float s = step; s <= (float)((double)length - step); s = (float)((double)s + step))
		{
			var q = a + dir * s;
			float land = Continent(q.x, q.y);
			if (land > 0.4f)
			{
				return false;
			}
			allWater &= !(land > 0.05f);
		}
		return !allWater;
	}

	// ---- Streams: 3000 tries for a start on low slopes (26 to 31 m) and an end higher up (36 to 44 m,
	// 80 to 200 m away), their middle between those heights.

	private List<River> PlaceStreams(bool deepNorth)
	{
		var saved = Rnd.state;
		Rnd.InitState(_streamSeed);
		var streams = new List<River>();
		for (int i = 0; i < 3000; i++)
		{
			if (!StreamStart(!deepNorth, out var start) || !StreamEnd(start, !deepNorth, out var end))
			{
				continue;
			}
			var middle = (start + end) * 0.5f;
			float h = GetPregenerationHeight(middle.x, middle.y, !deepNorth);
			if (h < 26f || h > 44f)
			{
				continue;
			}
			var stream = new River { P0 = start, P1 = end, Center = middle, WidthMax = 20f, WidthMin = 20f };
			Curves(stream);
			streams.Add(stream);
		}
		Draw(streams, deepNorth ? Streams.DeepNorthOnly : Streams.NotDeepNorth);
		Rnd.state = saved;
		return streams;
	}

	private bool StreamStart(bool forRivers, out Vector2 start)
	{
		for (int i = 0; i < 100; i++)
		{
			float x = Rnd.Range(-10000f, 10000f), z = Rnd.Range(-10000f, 10000f);
			float h = GetPregenerationHeight(x, z, forRivers);
			if (h > 26f && h < 31f)
			{
				start = new Vector2(x, z);
				return true;
			}
		}
		start = Vector2.zero;
		return false;
	}

	// Tried at random directions, from 200 m down towards 80 m.
	private bool StreamEnd(Vector2 start, bool forRivers, out Vector2 end)
	{
		const float longest = 200f, shortest = 80f;
		const int tries = 100;
		float step = (float)(((double)longest - shortest) / tries);
		float length = longest;
		for (int i = 0; i < tries; i++)
		{
			length = (float)((double)length - step);
			float angle = Rnd.Range(0f, MathF.PI * 2f);
			var q = start + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * length;
			float h = GetPregenerationHeight(q.x, q.y, forRivers);
			if (h > 36f && h < 44f)
			{
				end = q;
				return true;
			}
		}
		end = Vector2.zero;
		return false;
	}

	// ---- Drawing them into the cells.

	// Points every eighth of the narrowest width along each line, waved sideways, each a random width;
	// added to the cells after those drawn before (the order the ground sums them in).
	private void Draw(List<River> rivers, Streams which)
	{
		var cells = new Dictionary<Vector2i, List<RiverPoint>>();
		foreach (var river in rivers)
		{
			if (which != Streams.All && IsDeepnorth(river.P0.x, river.P0.y) != (which == Streams.DeepNorthOnly))
			{
				continue;
			}
			float step = (float)((double)river.WidthMin / 8.0);
			var dir = (river.P1 - river.P0).normalized;
			var side = new Vector2(0f - dir.y, dir.x);
			float length = Vector2.Distance(river.P0, river.P1);
			for (float along = 0f; along <= length; along = (float)((double)along + step))
			{
				float u = (float)((double)along / river.CurveWavelength);
				float sway = (float)(Math.Sin(u) * Math.Sin((double)u * (double)0.63412f) * Math.Sin((double)u * (double)0.33412f) * river.CurveWidth);
				float width = Rnd.Range(river.WidthMin, river.WidthMax);
				AddToCells(cells, river.P0 + dir * along + side * sway, width);
			}
		}
		foreach (var (cell, added) in cells)
		{
			_riverCells[cell] = _riverCells.TryGetValue(cell, out var before) ? [.. before, .. added] : added.ToArray();
		}
	}

	// A point into every cell its circle reaches (rows south to north, cells west to east).
	private static void AddToCells(Dictionary<Vector2i, List<RiverPoint>> cells, Vector2 p, float width)
	{
		var home = GetRiverGrid(p.x, p.y);
		int reach = Mathf.CeilToInt((float)((double)width / 64.0));
		for (int y = home.y - reach; y <= home.y + reach; y++)
		{
			for (int x = home.x - reach; x <= home.x + reach; x++)
			{
				var cell = new Vector2i(x, y);
				if (!InsideRiverGrid(cell, p, width))
				{
					continue;
				}
				if (!cells.TryGetValue(cell, out var list))
				{
					cells.Add(cell, list = new());
				}
				list.Add(new RiverPoint(p, width));
			}
		}
	}

	// Whether a circle reaches into a cell (the cell and the circle's box overlap).
	public static bool InsideRiverGrid(Vector2i cell, Vector2 p, float r)
	{
		var away = p - new Vector2((float)((double)cell.x * 64.0), (float)((double)cell.y * 64.0));
		float reach = (float)((double)r + 32.0);
		return Math.Abs(away.x) < reach && Math.Abs(away.y) < reach;
	}

	public static Vector2i GetRiverGrid(float x, float z) =>
		new(Mathf.FloorToInt((float)(((double)x + 32.0) / 64.0)), Mathf.FloorToInt((float)(((double)z + 32.0) / 64.0)));

	// How deep in a river a point is (0 outside, 1 on a point's line) and the rivers' width there (the
	// widths averaged by depth).
	private void RiverAt(float x, float z, out float weight, out float width)
	{
		weight = 0f;
		width = 0f;
		if (!_riverCells.TryGetValue(GetRiverGrid(x, z), out var points))
		{
			return;
		}
		var at = new Vector2(x, z);
		float widths = 0f, depths = 0f;
		foreach (var rp in points)
		{
			float d2 = Vector2.SqrMagnitude(rp.At - at);
			if (!(d2 < rp.WidthSquared))
			{
				continue;
			}
			float depth = (float)(1.0 - (double)(float)Math.Sqrt(d2) / rp.Width);
			if (depth > weight)
			{
				weight = depth;
			}
			widths = (float)((double)widths + (double)rp.Width * depth);
			depths = (float)((double)depths + depth);
		}
		if (depths > 0f)
		{
			width = (float)((double)widths / depths);
		}
	}
}
