
namespace TerrainEditor.Desktop;

/// <summary>
/// The sculpt and paint tools, as the web editor applies them every frame while the button is held (editor.html
/// applyBrush), with the brush shapes and falloffs of editor/brushes.js.
/// </summary>
public enum BrushTool
{
    /// <summary>Lifts the ground under the brush.</summary>
    Raise,
    /// <summary>Digs the ground down under the brush.</summary>
    Lower,
    /// <summary>Levels the ground to one height (the brush's Target, or where the stroke starts).</summary>
    Flatten,
    /// <summary>Evens out bumps and sharp edges.</summary>
    Smooth,
    /// <summary>Naturalize: flat, tool-made ground towards natural-looking bumps.</summary>
    Natural,
    /// <summary>Weathering: Thermal (slopes slide to the rest angle) or Water (rain).</summary>
    Erode,
    /// <summary>Back towards the ground as the world generated it.</summary>
    Restore,
    /// <summary>Paints bare dirt, like a path.</summary>
    PaintDirt,
    /// <summary>Paints cultivated soil for planting.</summary>
    PaintCultivated,
    /// <summary>Paints paved stone ground.</summary>
    PaintPaved,
    /// <summary>Removes paint: back to the biome's own ground.</summary>
    PaintClear,
}
