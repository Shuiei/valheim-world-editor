using System.Numerics;

namespace TerrainEditor.Desktop;

// Where an object may stand: on the block and not masked out, and how far it must keep from others.
public sealed partial class PlaceTool
{
    /// <summary>Whether a grid point is on the block (a metre in from the edges) and not masked out.</summary>
    internal static bool OnBlockAndMask(WorldScene s, Vector2 g, Func<int, float>? mask) =>
        !(g.X < 1 || g.Y < 1 || g.X > s.W - 2 || g.Y > s.H - 2) && (mask == null || !(mask((int)MathF.Round(g.Y) * s.W + (int)MathF.Round(g.X)) <= 0));

    /// <summary>
    /// The distance to keep from other objects for a kind: with one at a time you pick the spot, so only an object right on
    /// top (0.3 m) blocks it, and a piece that snaps to the pieces there goes where the snapping puts it (On top: right over
    /// the one below). Otherwise the spacing.
    /// </summary>
    internal float SpacingFor(string name) => OneAtATime ? (SnapTo && IsPiece(name) && Mode == Modes.Brush ? 0 : 0.3f) : Spacing;
}
