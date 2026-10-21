
namespace TerrainEditor.Desktop;

/// <summary>How a material is drawn (BundleModels.Modes.cs): the cut-out, see-through and half-cut modes, and what is foliage.</summary>
internal sealed partial class BundleModels
{
    /// <summary>
    /// Whether a material is cut out, see-through or cut at half (see <see cref="ViewMaterial"/>), from its shader and its
    /// _Mode.
    /// </summary>
    /// <param name="shader">The shader's name.</param>
    /// <param name="mode">The material's _Mode.</param>
    /// <param name="vegetation">Whether the shader is a vegetation one.</param>
    /// <returns>Cut out, see-through, and cut at half (only when neither of the others).</returns>
    internal static (bool Cut, bool SeeThrough, bool HalfCut) Modes(string shader, float mode, bool vegetation)
    {
        bool cut = mode == 1 || vegetation || shader.Contains("Grass", StringComparison.Ordinal) || shader.Contains("Cutout", StringComparison.OrdinalIgnoreCase);
        bool standard = shader.StartsWith("Standard", StringComparison.Ordinal) || shader.StartsWith("Autodesk Interactive", StringComparison.Ordinal);
        bool glass = shader.Contains("Glass", StringComparison.OrdinalIgnoreCase);
        bool seeThrough = !cut && (standard && mode is 2 or 3 || glass
            || shader.Contains("transparent", StringComparison.OrdinalIgnoreCase) || shader.Contains("Particles", StringComparison.Ordinal));
        bool halfCut = !cut && !seeThrough && mode is 2 or 3;
        return (cut, seeThrough, halfCut);
    }

    /// <summary>
    /// Whether a material is foliage: drawn with the game's vegetation or grass shaders (leaves, bushes, grass; hanging
    /// cloth too), which a click counts only where nothing solid is behind it. A cut-out piece (a bed's fur, a hide rug)
    /// is not.
    /// </summary>
    /// <param name="shader">The shader's name.</param>
    internal static bool Foliage(string shader) =>
        shader.Contains("Vegetation", StringComparison.Ordinal) || shader.Contains("Grass", StringComparison.Ordinal);
}
