using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using Vector4 = System.Numerics.Vector4;

namespace TerrainEditor.Desktop;

/// <summary>
/// The zone filter's matches and the zones marked for reset, filled over the map.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// The zone filter's matches (blue) and the zones marked for reset (red).
    /// </summary>
    private void DrawZoneFills()
    {
        var s = _session;
        if (s == null)
        {
            return;
        }
        // Made again only when the edits (their resets) or the matches changed: the same while the map idles.
        var key = (s, s.Edits.Version, Matches);
        if (_zoneFillsKey == key)
        {
            Fill(_zoneFills, PrimitiveType.Triangles);
            return;
        }
        var marked = s.Edits.Resets.Select(r => (r.X, r.Z)).ToHashSet();
        var data = new List<float>();
        void Zone(int x, int z, Vector4 col) => Quad(data, new(x * 64 - 32, z * 64 - 32), new(x * 64 + 32, z * 64 - 32), new(x * 64 + 32, z * 64 + 32), new(x * 64 - 32, z * 64 + 32), col);
        foreach (var (x, z) in Matches)
        {
            if (!marked.Contains((x, z)))
            {
                Zone(x, z, new Vector4(70 / 255f, 160 / 255f, 1, 0.35f));
            }
        }
        foreach (var (x, z) in marked)
        {
            Zone(x, z, new Vector4(1, 70 / 255f, 50 / 255f, 0.45f));
        }
        (_zoneFillsKey, _zoneFills) = (key, [.. data]);
        Fill(_zoneFills, PrimitiveType.Triangles);
    }

    /// <summary>What the zone fills were last made for: the session, its edits' version and the matches.</summary>
    private (WorldSession?, int, IReadOnlyList<(int X, int Z)>?) _zoneFillsKey;

    /// <summary>The zone fills last made (<see cref="DrawZoneFills"/>).</summary>
    private float[] _zoneFills = [];
}
