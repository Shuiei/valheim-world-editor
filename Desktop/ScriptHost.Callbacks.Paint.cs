using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TerrainEditor.Desktop;

// The Paint callback of a run.
public static partial class ScriptHost
{
    /// <summary>The paint callback: the ground's paint moved towards a kind's colour.</summary>
    private sealed partial class Callbacks
    {
        /// <summary>Paint: moves the paint of the point under (x, z) towards the kind's colour, by the strength (0 to 1).</summary>
        public void PaintAt(float x, float z, string kind, float strength)
        {
            float[] colour = kind.ToLowerInvariant() switch
            {
                "dirt" => Brush.PaintOf(BrushTool.PaintDirt)!,
                "cultivated" or "cultivate" => Brush.PaintOf(BrushTool.PaintCultivated)!,
                "paved" => Brush.PaintOf(BrushTool.PaintPaved)!,
                "clear" or "none" => Brush.PaintOf(BrushTool.PaintClear)!,
                _ => throw new ArgumentException($"no paint “{kind}” (dirt, cultivated, paved or clear)"),
            };
            int p = Point(x, z);
            if (p < 0 || _snap.Locked[p])
            {
                return;
            }
            lock (_ch)
            {
                float[] now = _ch.Paint.TryGetValue(p, out float[]? c) ? c : [.. Enumerable.Range(0, 4).Select(i => _snap.Mask[p * 4 + i] / 255f)];
                float k = Math.Clamp(strength, 0, 1);
                _ch.Paint[p] = [.. now.Select((v, i) => v + (colour[i] - v) * k)];
            }
        }
    }
}
