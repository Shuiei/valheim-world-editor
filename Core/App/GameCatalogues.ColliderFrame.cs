// The frame a collider's object sits in, as the piece scans carry it down a piece (see GameCatalogues).

using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// Where an object sits within its piece: the origin, the rotation and the scale that carry its own points into
    /// the piece's frame.
    /// </summary>
    /// <param name="Origin">The object's origin in the piece's frame.</param>
    /// <param name="Rotation">The object's rotation within the piece.</param>
    /// <param name="Scaling">The object's scale, as its parents multiply it.</param>
    internal readonly record struct ColliderFrame(V3 Origin, Quat Rotation, V3 Scaling)
    {
        /// <summary>
        /// A point of the object's own frame, in the piece's frame.
        /// </summary>
        /// <param name="local">The point, in the object's own frame.</param>
        public V3 Point(V3 local) => Add(Rotation.Rotate(Scale(local, Scaling)), Origin);
    }
}
