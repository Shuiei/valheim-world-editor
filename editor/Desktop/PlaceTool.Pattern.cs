using System.Numerics;

namespace TerrainEditor.Desktop;

// The brush's layout: the random pattern under the cursor, kept until the settings change.
public sealed partial class PlaceTool
{
    /// <summary>
    /// Brush: the layout under the cursor (offsets from it, with their draws), kept until the settings change or R.
    /// </summary>
    private List<(Vector2 D, Draw Draw)> _pattern = [];

    /// <summary>The settings the brush layout was made for (empty: make a new one).</summary>
    private string _patternKey = "";

    /// <summary>The settings the brush layout depends on.</summary>
    private string SettingsKey => $"{Brush.Radius}|{Density}|{Spacing}|{OneAtATime}|{string.Join(",", Chosen)}";

    /// <summary>
    /// New random choices (R): a new brush layout, new draws for shapes, new zone spots and new clumping.
    /// </summary>
    public void NewLayout()
    {
        _patternKey = "";
        _draws.Clear();
        _scatter = null;
        _clumpSeed = _rand.NextSingle() * 1000;
        Notify();
    }

    /// <summary>
    /// Makes the brush layout: one object at the cursor (One at a time), or random points in the disc at least the
    /// spacing apart (one at least, so a click with a small brush places something; 400 at most).
    /// </summary>
    private void MakePattern()
    {
        float r = Brush.Radius;
        int n = Math.Clamp((int)MathF.Round(Density / 100 * MathF.PI * r * r), 1, 400);
        _pattern = [];
        if (OneAtATime)
        {
            _pattern.Add((Vector2.Zero, NewDraw()));
        }
        else
        {
            // Dart throwing: random points in the disc, at least the spacing apart.
            for (int tries = 0; _pattern.Count < n && tries < n * 30; tries++)
            {
                float a = _rand.NextSingle() * MathF.Tau, dist = MathF.Sqrt(_rand.NextSingle()) * r;
                var p = new Vector2(MathF.Cos(a) * dist, MathF.Sin(a) * dist);
                if (_pattern.Any(q => Vector2.Distance(q.D, p) < Spacing))
                {
                    continue;
                }
                _pattern.Add((p, NewDraw()));
            }
        }
        _patternKey = SettingsKey;
    }
}
