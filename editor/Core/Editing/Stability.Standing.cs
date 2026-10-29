using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Stability
{
    /// <summary>
    /// The building pieces something stands on: their colliders in the world (the boxes the support check sees, a
    /// ship's hull and a door's leaf aside), ready to say whether a point is inside one and where its top is.
    /// </summary>
    public sealed class Standing
    {
        /// <summary>A point this near a box's face is on it, not inside.</summary>
        private const float Edge = 1e-3f;
        /// <summary>How many pieces one over the other are climbed at most (a creature in a stack of floors).</summary>
        private const int Stack = 16;
        /// <summary>The pieces' colliders in the world.</summary>
        private readonly List<Obb> _boxes;

        /// <summary>The colliders of the pieces that have support data; others have no boxes.</summary>
        /// <param name="pieces">The pieces, where they stand.</param>
        public Standing(IReadOnlyList<Piece> pieces)
        {
            var catalogue = _pieces;
            _boxes = [.. pieces.SelectMany(p => catalogue.GetValueOrDefault(p.Prefab)?.Boxes.Where(b => b.Counts && !b.Body).Select(b => World(b, p, 0f)) ?? [])];
        }

        /// <summary>
        /// The height an object's origin is lifted to so it stands on the piece it is in: the top, along the vertical
        /// through the point, of the highest box that holds it (strictly inside: on a face is not in), and from there
        /// again while another box holds it, as a stack of pieces may. Null when no box holds the point: it stays.
        /// </summary>
        /// <param name="origin">The object's origin in the world.</param>
        public float? Top(Vector3 origin)
        {
            float? lifted = null;
            for (int climbed = 0; climbed < Stack; climbed++)
            {
                var at = lifted is float y ? origin with { Y = y } : origin;
                float? top = null;
                foreach (var box in _boxes)
                {
                    if (TopThrough(box, at) is float t && (top is null || t > top))
                    {
                        top = t;
                    }
                }
                if (top is null)
                {
                    return lifted;
                }
                lifted = top;
            }
            return lifted;
        }

        /// <summary>
        /// Where the vertical through a point leaves a box upwards (world height), when the point is strictly inside
        /// the box; null when it is not.
        /// </summary>
        private static float? TopThrough(in Obb box, Vector3 p)
        {
            var d = p - box.C;
            // The point and the vertical in the box's own frame.
            Vector3 a = new(Vector3.Dot(d, box.X), Vector3.Dot(d, box.Y), Vector3.Dot(d, box.Z));
            Vector3 up = new(box.X.Y, box.Y.Y, box.Z.Y);
            if (MathF.Abs(a.X) >= box.H.X - Edge || MathF.Abs(a.Y) >= box.H.Y - Edge || MathF.Abs(a.Z) >= box.H.Z - Edge)
            {
                return null;
            }
            // Along the vertical, how far up until a face of the box is reached (the nearest face wins).
            float t = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float b = up[i];
                if (MathF.Abs(b) > 1e-6f)
                {
                    t = MathF.Min(t, (b > 0 ? box.H[i] - a[i] : -box.H[i] - a[i]) / b);
                }
            }
            return p.Y + t;
        }
    }
}
