using System.Numerics;
using ValheimGen;
using Vector3 = System.Numerics.Vector3;

namespace TerrainEditor.Desktop;

/// <summary>
/// The colours the game look's water is drawn with (linear): the editor's own, or Expand World Data's water colours
/// where the camera is. The editor's water is its own drawing, not the game's water shader, so the mod's colours are
/// laid on it as close as it goes: the top colour is the water's in the shallows, the bottom colour the deep water's,
/// the shallow colour tints the water near the shore, the surface colour tints the sky's reflection (its hue only).
/// </summary>
/// <param name="Shallow">The water's colour where it is shallow.</param>
/// <param name="Deep">The deep water's colour.</param>
/// <param name="Shore">The tint near the shore, its alpha how much (0: none).</param>
/// <param name="Surface">The sky reflection's tint.</param>
internal readonly record struct WaterTint(Vector3 Shallow, Vector3 Deep, Vector4 Shore, Vector3 Surface)
{
    /// <summary>The editor's own water.</summary>
    internal static readonly WaterTint Own = new(new(0.23f, 0.42f, 0.36f), new(0.012f, 0.055f, 0.085f), Vector4.Zero, Vector3.One);

    /// <summary>The water with these colours, the editor's own for each one not given.</summary>
    /// <param name="c">The mod's colours.</param>
    internal static WaterTint Of(WaterColours c) => c.IsNone ? Own : new(
        c.Top is { } top ? Linear(top) : Own.Shallow,
        c.Bottom is { } bottom ? Linear(bottom) : Own.Deep,
        c.Shallow is { } shallow ? new Vector4(Linear(shallow), 1f) : Own.Shore,
        c.Surface is { } surface ? Hue(surface) : Own.Surface);

    /// <summary>A colour (as the file gives it, each channel taken between 0 and 1) as the linear values the shader takes.</summary>
    private static Vector3 Linear(Rgba c) => Shaders.Linear(Math.Clamp(c.R, 0f, 1f), Math.Clamp(c.G, 0f, 1f), Math.Clamp(c.B, 0f, 1f));

    /// <summary>A colour's hue: its linear colour scaled so its brightest channel is 1; white for black.</summary>
    private static Vector3 Hue(Rgba c)
    {
        var l = Linear(c);
        float most = MathF.Max(MathF.Max(l.X, l.Y), l.Z);
        return most > 0f ? l / most : Vector3.One;
    }
}
