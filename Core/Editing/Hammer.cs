using System.Numerics;

namespace TerrainEditor.Editing;

/// <summary>
/// The game's hammer, as it places a piece (Player.UpdatePlacementGhost, PieceRayTest,
/// FindClosestSnapPoints; Piece.GetSnapPoints), for the Workshop's Build: the build ray hits the nearest
/// piece collider or the ground; the ghost, turned as asked, is put 50 m out along the hit's normal, then
/// moved so its collider point nearest the hit touches it (ground pieces and "clip everything" pieces:
/// their origin at the hit); then, of every snap point of the ghost and every snap point of the pieces
/// within 10 m, the closest pair within 0.5 m meets, unless that puts it right where the same piece
/// already stands. The pieces' colliders, snap points and flags are read from the player's own Valheim
/// (GameData; the tests use tests/fixtures/piece-place.json). All in world space (Unity's axes).
/// </summary>
public static partial class Hammer
{
    /// <summary>Snap points this close (metres) meet; pieces whose reach comes this close (metres) are searched.</summary>
    public const float SnapDistance = 0.5f, SnapRadius = 10f;

    /// <summary>A piece's collider, in the piece's own frame.</summary>
    public abstract record Shape;
    /// <summary>A box collider: centre, half size, rotation.</summary>
    /// <param name="C">Its centre.</param>
    /// <param name="H">Its half size.</param>
    /// <param name="Q">Its rotation in the piece's frame.</param>
    public sealed record Box(Vector3 C, Vector3 H, Quaternion Q) : Shape;
    /// <summary>A sphere collider.</summary>
    /// <param name="C">Its centre.</param>
    /// <param name="R">Its radius.</param>
    public sealed record Sphere(Vector3 C, float R) : Shape;
    /// <summary>A capsule collider: the two ends of its axis and its radius.</summary>
    /// <param name="A">One end of its axis.</param>
    /// <param name="B">The other end.</param>
    /// <param name="R">Its radius.</param>
    public sealed record Capsule(Vector3 A, Vector3 B, float R) : Shape;
    /// <summary>A mesh collider.</summary>
    /// <param name="Convex">Unity takes it as convex (only then does ClosestPoint work on it).</param>
    /// <param name="V">Its vertices.</param>
    /// <param name="I">Its triangles, three vertex indices each.</param>
    public sealed record Mesh(bool Convex, Vector3[] V, int[] I) : Shape;

    /// <summary>What the hammer needs of a piece.</summary>
    /// <param name="Ground">A ground piece (Piece.m_groundPiece): put with its origin at a ground hit.</param>
    /// <param name="ClipGround">May clip into the ground (Piece.m_clipGround): put with its origin at a ground hit.</param>
    /// <param name="ClipEverything">May clip into anything (Piece.m_clipEverything): put with its origin at any hit.</param>
    /// <param name="RotatedOverlap">May stand where the same piece does if turned differently (Piece.m_allowRotatedOverlap).</param>
    /// <param name="Snaps">Its snap points, in its own frame.</param>
    /// <param name="Colliders">Its colliders, in its own frame.</param>
    public sealed record Data(bool Ground, bool ClipGround, bool ClipEverything, bool RotatedOverlap, Vector3[] Snaps, Shape[] Colliders)
    {
        /// <summary>The piece's reach from its origin (for the 10 m search).</summary>
        public float Radius { get; } = Colliders.Select(Reach).DefaultIfEmpty(0).Max();
    }

    /// <summary>How far a collider reaches from the piece's origin, at most (metres).</summary>
    private static float Reach(Shape s) => s switch
    {
        Box b => b.C.Length() + b.H.Length(),
        Sphere sp => sp.C.Length() + sp.R,
        Capsule c => MathF.Max(c.A.Length(), c.B.Length()) + c.R,
        Mesh m => m.V.Select(v => v.Length()).DefaultIfEmpty(0).Max(),
        _ => 0,
    };

    /// <summary>None until Use: no world or Workshop opens before then (GameData).</summary>
    private static volatile Dictionary<string, Data> _all = [];
}
