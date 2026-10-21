using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// A world's ground as a world generation mod lays it out, answering in place of the game's GetBiome, GetBiomeHeight,
/// GetAshlandsHeight and GetForestFactor (whose bodies the mod's patches skip). The points come as the game's callers
/// give them, after other mods' prefixes (Expand World's stretch); the editor applies other mods' postfixes to the
/// answers. Asked from many threads at once.
/// </summary>
public interface IGroundLayout
{
    /// <summary>The biome at a point, as the game's Heightmap.Biome value (Meadows 1, Swamp 2, Mountain 4, …).</summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    int Biome(float x, float z);

    /// <summary>The ground's height at a point, metres, with its mask (the game's ground paint channels, 0 to 1).</summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="mask">The mask's red, green, blue and alpha as X, Y, Z and W.</param>
    float Height(float x, float z, out Vector4 mask);

    /// <summary>The forest factor at a point (lower: denser), as the game's GetForestFactor returns it.</summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    float Forest(float x, float z);
}
