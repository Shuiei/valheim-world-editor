using System.Numerics;

namespace TerrainEditor.Desktop;

// The Measure tool, like the web editor's (editor/measure.js): a tape between two points of the ground.
// The first click sets A, the line follows the mouse, the second click fixes B; a third click starts
// again. Points are world positions (x east, height, z north).
public sealed class MeasureTool
{
	public Vector3? A { get; private set; }
	public Vector3? B { get; private set; }
	public bool Fixed { get; private set; }
	public event Action? Changed;

	public void Down(Vector3 at)
	{
		if (A == null || Fixed)
		{
			A = at;
			B = null;
			Fixed = false;
		}
		else
		{
			B = at;
			Fixed = true;
		}
		Changed?.Invoke();
	}

	public void Move(Vector3 at)
	{
		if (A != null && !Fixed)
		{
			B = at;
			Changed?.Invoke();
		}
	}

	public void Clear()
	{
		A = B = null;
		Fixed = false;
		Changed?.Invoke();
	}

	public sealed record Result(float Distance, float AlongSlope, float HeightA, float HeightB, float Rise, float? SlopePercent, float SlopeDegrees, float Lowest, float Highest, float? WaterDepth);

	// What the tape measures; heightAt gives the ground's height at a world point (for the lowest and
	// highest points along it).
	public Result? Measure(Func<float, float, float> heightAt, float water)
	{
		if (A is not { } a || B is not { } b)
		{
			return null;
		}
		float flat = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z)), rise = b.Y - a.Y;
		float lo = float.MaxValue, hi = float.MinValue;
		int n = Math.Max(2, (int)MathF.Ceiling(flat));
		for (int i = 0; i <= n; i++)
		{
			float h = heightAt(a.X + (b.X - a.X) * i / n, a.Z + (b.Z - a.Z) * i / n);
			lo = MathF.Min(lo, h);
			hi = MathF.Max(hi, h);
		}
		float low = MathF.Min(a.Y, b.Y);
		return new Result(flat, MathF.Sqrt(flat * flat + rise * rise), a.Y, b.Y, rise, flat > 0.01f ? rise / flat * 100 : null,
			MathF.Atan2(MathF.Abs(rise), flat) * 180 / MathF.PI, lo, hi, low < water ? water - low : null);
	}

	// What to show: the hint and the measured values (label, value).
	public (string Hint, List<(string, string)> Rows) Describe(Func<float, float, float> heightAt, float water)
	{
		if (A == null)
		{
			return ("Click a first point on the ground.", new());
		}
		if (Measure(heightAt, water) is not { } r)
		{
			return ("Click a second point.", new());
		}
		return (Fixed ? "Click again to start a new measurement." : "Click to fix the second point.", new()
		{
			("Distance", $"{r.Distance:0.0} m"),
			("Along the slope", $"{r.AlongSlope:0.0} m"),
			("Height A → B", $"{r.HeightA:0.0} → {r.HeightB:0.0} m ({(r.Rise >= 0 ? "+" : "")}{r.Rise:0.0})"),
			("Slope", $"{(r.SlopePercent is float p ? $"{p:0} %" : "–")} · {r.SlopeDegrees:0.0}°"),
			("Lowest / highest", $"{r.Lowest:0.0} / {r.Highest:0.0} m"),
			("Water depth", r.WaterDepth is float d ? $"{d:0.0} m" : "above sea"),
		});
	}
}
