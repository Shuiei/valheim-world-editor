
namespace TerrainEditor.Desktop;

/// <summary>
/// The brush's shape: Ring works on a band near the edge, Noise has a ragged edge, Stamp uses a picture
/// (<see cref="Brush.StampData"/>).
/// </summary>
public enum BrushShape
{
    /// <summary>A round brush.</summary>
    Circle,
    /// <summary>A square brush.</summary>
    Square,
    /// <summary>Works on a band near the edge.</summary>
    Ring,
    /// <summary>A round brush with a ragged edge.</summary>
    Noise,
    /// <summary>Shaped by a picture (<see cref="Brush.StampData"/>).</summary>
    Stamp,
}
