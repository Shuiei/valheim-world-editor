using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Place tool, like the web editor's (editor/plant.js): any kind of object, painted with a brush (a density and a
/// minimum spacing, like WorldPainter's tree layers; Shift + drag removes the chosen kinds), or along lines, circles
/// and rectangles (pieces end to end at their snap points, like the hammer), in grids and in zones. Positions are grid
/// points of the block (x east, z north).
/// </summary>
public sealed partial class PlaceTool
{
    /// <summary>
    /// How objects are laid out: painted with a brush, along a line (or circle, rectangle), in a grid, or scattered in
    /// a zone.
    /// </summary>
    public enum Modes
    {
        /// <summary>Painted with a brush.</summary>
        Brush,
        /// <summary>Along a line, circle or rectangle.</summary>
        Line,
        /// <summary>In a grid.</summary>
        Grid,
        /// <summary>Scattered in a zone.</summary>
        Zone,
    }
    /// <summary>
    /// Line mode: points clicked along the way, a circle (centre and radius) or a rectangle (two corners).
    /// </summary>
    public enum LineShapes
    {
        /// <summary>Points clicked along the way.</summary>
        Points,
        /// <summary>A circle: centre and radius.</summary>
        Circle,
        /// <summary>A rectangle: two corners.</summary>
        Rect,
    }
    /// <summary>
    /// Where objects stand: on the ground, <c>Elev</c> metres above it, or all at the one height <c>Elev</c>.
    /// </summary>
    public enum Elevations
    {
        /// <summary>On the ground.</summary>
        Ground,
        /// <summary><c>Elev</c> metres above the ground.</summary>
        Above,
        /// <summary>All at the one height <c>Elev</c>.</summary>
        At,
    }

    // ---- What the tool needs from the scene (set by the window).

    // ---- Settings.

    // ---- Where objects can go.

    // ---- Brush.

    // ---- Line, grid and zone shapes.

    /// <summary>Zone: the spots, with a key of the zone and settings they were made for.</summary>
    private (float Key, List<Vector2> Pts)? _scatter;
}
