
namespace TerrainEditor.Desktop;

/// <summary>How the brush fades from its middle to its edge (see <see cref="Brush.Fade"/>).</summary>
public enum Falloff
{
    /// <summary>An S-curve (smoothstep): soft at both ends.</summary>
    Smooth,
    /// <summary>Straight from the middle to the edge.</summary>
    Linear,
    /// <summary>Round on top, falling fast near the edge.</summary>
    Dome,
    /// <summary>Full strength, fading only in the outer fifth.</summary>
    Flat,
    /// <summary>Strong only near the middle (cubic).</summary>
    Peak,
    /// <summary>Full strength right up to the edge: steep walls, like the game's pickaxe.</summary>
    Sharp,
}
