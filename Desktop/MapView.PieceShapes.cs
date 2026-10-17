using Avalonia.OpenGL.Controls;
using TerrainEditor.Terrain;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace TerrainEditor.Desktop;

/// <summary>
/// The shapes of the player-built pieces and ruins over the map.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// The web map's piece colours per category (fill, edge): misc, crafting, wood, stone, furniture.
    /// </summary>
    private static readonly (Vector4 Fill, Vector4 Edge)[] PieceStyle =
    [
        (new(150 / 255f, 72 / 255f, 34 / 255f, 0.92f), new(60 / 255f, 25 / 255f, 10 / 255f, 0.9f)),
        (new(128 / 255f, 56 / 255f, 40 / 255f, 0.9f), new(55 / 255f, 22 / 255f, 14 / 255f, 0.9f)),
        (new(92 / 255f, 64 / 255f, 40 / 255f, 0.88f), new(38 / 255f, 24 / 255f, 14 / 255f, 0.9f)),
        (new(110 / 255f, 104 / 255f, 96 / 255f, 0.92f), new(45 / 255f, 42 / 255f, 38 / 255f, 0.9f)),
        (new(140 / 255f, 102 / 255f, 62 / 255f, 0.8f), new(60 / 255f, 40 / 255f, 22 / 255f, 0.85f)),
    ];

    /// <summary>
    /// Adds one piece's shape: a footprint (a box, or its turned quad with the outline when close), or a dot when far.
    /// </summary>
    /// <param name="fill">Where the fill's corners go.</param>
    /// <param name="edge">Where the outline's segments go (close up only).</param>
    /// <param name="pc">The pieces' floats the piece is in (built or ruins).</param>
    /// <param name="o">The piece's first float in <paramref name="pc"/>.</param>
    /// <param name="faint">Whether the piece is a ruin not joined to a building (drawn fainter).</param>
    /// <param name="kind">The zoom kind: 0 dots, 1 footprints, 2 footprints with outlines.</param>
    /// <param name="mpp">Metres per map pixel of the view.</param>
    private static void AddPiece(List<float> fill, List<float> edge, float[] pc, int o, bool faint, int kind, float mpp)
    {
        float px = pc[o], pz = pc[o + 2];
        var style = PieceStyle[Math.Clamp((int)pc[o + 9], 0, PieceStyle.Length - 1)];
        if (faint)
        {
            style = (style.Fill with { W = style.Fill.W * 0.55f }, style.Edge with { W = style.Edge.W * 0.55f });
        }
        if (kind == 0)
        {
            float r = mpp;
            Quad(fill, new(px - r, pz - r), new(px + r, pz - r), new(px + r, pz + r), new(px - r, pz + r), style.Edge);
            return;
        }
        // Unity Y rotation (clockwise seen from above): local (x, z) to world.
        float a = pc[o + 3] * MathF.PI / 180, c = MathF.Cos(a), sn = MathF.Sin(a);
        Vector2 W(float lx, float lz) => new(px + lx * c + lz * sn, pz - lx * sn + lz * c);
        Vector2 p0 = W(pc[o + 4], pc[o + 6]), p1 = W(pc[o + 5], pc[o + 6]), p2 = W(pc[o + 5], pc[o + 7]), p3 = W(pc[o + 4], pc[o + 7]);
        Quad(fill, p0, p1, p2, p3, style.Fill);
        if (kind == 2)
        {
            foreach (var (p, q) in new[] { (p0, p1), (p1, p2), (p2, p3), (p3, p0) })
            {
                edge.AddRange([p.X, p.Y, style.Edge.X, style.Edge.Y, style.Edge.Z, style.Edge.W, q.X, q.Y, style.Edge.X, style.Edge.Y, style.Edge.Z, style.Edge.W]);
            }
        }
    }

    /// <summary>
    /// Adds the shapes of every piece shown: the player-built ones, and the ruins that are shown (joined ones with the
    /// buildings, the others fainter).
    /// </summary>
    /// <param name="fill">Where the fill's corners go.</param>
    /// <param name="edge">Where the outlines' segments go.</param>
    /// <param name="built">The player-built pieces' floats.</param>
    /// <param name="ruins">The ruins' floats.</param>
    /// <param name="kind">The zoom kind: 0 dots, 1 footprints, 2 footprints with outlines.</param>
    /// <param name="mpp">Metres per map pixel of the view.</param>
    private void AddPieces(List<float> fill, List<float> edge, float[] built, float[] ruins, int kind, float mpp)
    {
        foreach (var (ruin, i) in _pieceOrder)
        {
            // A ruin's piece joined to a building is drawn with it; the other ruins' fainter.
            bool joined = ruin && _ruinJoined[i], faint = ruin && !joined;
            if (!(faint ? ShowRuins : ShowBuildings))
            {
                continue;
            }
            AddPiece(fill, edge, ruin ? ruins : built, i * PieceCatalog.Stride, faint, kind, mpp);
        }
    }
}
