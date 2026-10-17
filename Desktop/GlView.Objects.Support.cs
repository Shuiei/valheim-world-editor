using System.Numerics;

namespace TerrainEditor.Desktop;

// The support check (the Workshop): each building piece's tint as the game's build mode shows its support.
public sealed partial class GlView
{
    /// <summary>
    /// The support check (the Workshop): each building piece tinted as the game's build mode shows its
    /// support (SupportTint). Per thing index: -1 full, 0..1, or -2 breaks. Null: off.
    /// </summary>
    private volatile Dictionary<int, float>? _support;

    /// <summary>Turns the support check on (values per thing index, see <c>_support</c>) or off (null).</summary>
    public void ShowSupport(Dictionary<int, float>? support)
    {
        _support = support;
        // The tints are in the instances: built again.
        lock (_groups)
        {
            foreach (var g in _groups.Values)
            {
                MarkDirty(g);
            }
        }
        Wake();
    }

    /// <summary>The support values shown, null when the check is off.</summary>
    internal Dictionary<int, float>? Support => _support;

    /// <summary>
    /// The game's colour for a support value (WearNTear.Highlight): light blue at full support (on the
    /// ground), else red to green as it gets stronger, more saturated and brighter the weaker; one that
    /// breaks as the weakest (red).
    /// </summary>
    internal static Vector3 SupportTint(float v)
    {
        if (v is > (-1.5f) and < 0)
        {
            return new Vector3(0.6f, 0.8f, 1f);
        }
        float t = Math.Clamp(v < 0 ? 0 : v, 0, 1);
        // Lerp(red, green, t) has hue 0..1/3 with full saturation and value: its HSV, then the game's S and V.
        float h = new Vector3(1 - t, t, 0) is var c && c.X >= c.Y ? c.Y / c.X / 6f : (2 - c.X / c.Y) / 6f;
        return HsvToRgb(h, 1 - 0.5f * t, 1.2f - 0.3f * t);
    }

    /// <summary>A colour from hue (0..1), saturation and value (value may go above 1).</summary>
    private static Vector3 HsvToRgb(float h, float s, float v)
    {
        float r = Math.Clamp(MathF.Abs(h * 6 - 3) - 1, 0, 1), g = Math.Clamp(2 - MathF.Abs(h * 6 - 2), 0, 1), b = Math.Clamp(2 - MathF.Abs(h * 6 - 4), 0, 1);
        return new Vector3((1 - s + s * r) * v, (1 - s + s * g) * v, (1 - s + s * b) * v);
    }
}
