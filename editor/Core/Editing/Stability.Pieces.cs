using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Stability
{
    /// <summary>A building piece to work out.</summary>
    /// <param name="Prefab">Its prefab name.</param>
    /// <param name="Position">Where it stands (world space).</param>
    /// <param name="Rotation">How it is turned.</param>
    /// <param name="Scale">Its uniform scale (0 or less is taken as 1).</param>
    public sealed record Piece(string Prefab, Vector3 Position, Quaternion Rotation, float Scale = 1f);

    /// <summary>What holds and what falls, per piece in the order given.</summary>
    /// <param name="Support">Its support.</param>
    /// <param name="Colour">
    /// Its colour value as the game shows it in build mode (-1: full, on the ground, blue; else 0 red, about
    /// to break, to 1 green).
    /// </param>
    /// <param name="Breaks">Whether it breaks.</param>
    /// <param name="Free">Pieces the game does not give support to (not counted).</param>
    /// <param name="Falls">Broken pieces (their indices), in the order they fall.</param>
    public sealed record Result(float[] Support, float[] Colour, bool[] Breaks, bool[] Free, List<int> Falls)
    {
        /// <summary>How many pieces break.</summary>
        public int Breaking => Falls.Count;
    }

    /// <summary>An oriented box in the world: its middle, its three axes (unit length) and its half size along them.</summary>
    internal readonly record struct Obb(Vector3 C, Vector3 X, Vector3 Y, Vector3 Z, Vector3 H)
    {
        /// <summary>Its eight corners.</summary>
        public IEnumerable<Vector3> Corners()
        {
            for (int i = 0; i < 8; i++)
            {
                yield return C + X * ((i & 1) != 0 ? H.X : -H.X) + Y * ((i & 2) != 0 ? H.Y : -H.Y) + Z * ((i & 4) != 0 ? H.Z : -H.Z);
            }
        }

        /// <summary>The radius of a sphere round it, from its middle.</summary>
        public float Radius => H.Length();

        /// <summary>The point of the box nearest to p (Collider.ClosestPoint).</summary>
        public Vector3 Closest(Vector3 p)
        {
            var d = p - C;
            return C + X * Math.Clamp(Vector3.Dot(d, X), -H.X, H.X) + Y * Math.Clamp(Vector3.Dot(d, Y), -H.Y, H.Y) + Z * Math.Clamp(Vector3.Dot(d, Z), -H.Z, H.Z);
        }
    }
}
