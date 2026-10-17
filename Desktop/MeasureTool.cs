using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Measure tool, like the web editor's (editor/measure.js): a tape between two points of the ground. The first
/// click sets A, the line follows the mouse, the second click fixes B; a third click starts again. Points are world
/// positions (x east, height, z north).
/// </summary>
public sealed class MeasureTool
{
    /// <summary>
    /// The first point, or null before the first click.
    /// </summary>
    public Vector3? A { get; private set; }
    /// <summary>
    /// The second point: follows the mouse until fixed; null until the mouse moves after A.
    /// </summary>
    public Vector3? B { get; private set; }
    /// <summary>
    /// Whether B is fixed by the second click.
    /// </summary>
    public bool Fixed { get; private set; }
    /// <summary>
    /// Raised whenever A, B or Fixed changes.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// A click on the ground: sets A (starting again after a fixed tape) or fixes B.
    /// </summary>
    /// <param name="at">The clicked world position.</param>
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

    /// <summary>
    /// The mouse moved: B follows it until fixed.
    /// </summary>
    public void Move(Vector3 at)
    {
        if (A != null && !Fixed)
        {
            B = at;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Empties the tape.
    /// </summary>
    public void Clear()
    {
        A = B = null;
        Fixed = false;
        Changed?.Invoke();
    }

    /// <summary>
    /// What the tape measures, in metres unless said.
    /// </summary>
    /// <param name="Distance">Flat distance, ignoring height.</param>
    /// <param name="AlongSlope">Straight distance from A to B, height included.</param>
    /// <param name="HeightA">A's height.</param>
    /// <param name="HeightB">B's height.</param>
    /// <param name="Rise">B's height minus A's.</param>
    /// <param name="SlopePercent">Rise over flat distance, in percent; null when the points are (almost) one above the
    /// other.</param>
    /// <param name="SlopeDegrees">The slope's angle, 0..90°, whichever way it goes.</param>
    /// <param name="Lowest">Lowest ground along the line, A and B included.</param>
    /// <param name="Highest">Highest ground along the line, A and B included.</param>
    /// <param name="WaterDepth">How far the lower point is under the water; null when both are above it.</param>
    public sealed record Result(float Distance, float AlongSlope, float HeightA, float HeightB, float Rise, float? SlopePercent, float SlopeDegrees, float Lowest, float Highest, float? WaterDepth);

    /// <summary>
    /// What the tape measures; heightAt gives the ground's height at a world point (for the lowest and highest points
    /// along it), sampled about every metre.
    /// </summary>
    /// <param name="heightAt">Ground height at a world (x, z).</param>
    /// <param name="water">The water level, in metres.</param>
    /// <returns>The measurements, or null until both points are set.</returns>
    public Result? Measure(Func<float, float, float> heightAt, float water)
    {
        if (A is not { } a || B is not { } b)
        {
            return null;
        }
        float flat = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z)), rise = b.Y - a.Y;
        // The clicked heights count too: the ground may have changed under them since.
        float lo = MathF.Min(a.Y, b.Y), hi = MathF.Max(a.Y, b.Y);
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

    /// <summary>
    /// What to show: the hint and the measured values (label, value).
    /// </summary>
    public (string Hint, List<(string, string)> Rows) Describe(Func<float, float, float> heightAt, float water)
    {
        if (A == null)
        {
            return ("Click a first point on the ground.", []);
        }
        if (Measure(heightAt, water) is not { } r)
        {
            return ("Click a second point.", []);
        }
        return (Fixed ? "Click again to start a new measurement." : "Click to fix the second point.",
        [
            ("Distance", $"{r.Distance:0.0} m"),
            ("Along the slope", $"{r.AlongSlope:0.0} m"),
            ("Height A → B", $"{r.HeightA:0.0} → {r.HeightB:0.0} m ({(r.Rise >= 0 ? "+" : "")}{r.Rise:0.0})"),
            ("Slope", $"{(r.SlopePercent is float p ? $"{p:0} %" : "–")} · {r.SlopeDegrees:0.0}°"),
            ("Lowest / highest", $"{r.Lowest:0.0} / {r.Highest:0.0} m"),
            ("Water depth", r.WaterDepth is float d ? $"{d:0.0} m" : "above sea"),
        ]);
    }
}
