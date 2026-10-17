using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// Where an item ends up: turned around the selection's middle (clockwise from above, like Unity's
    /// yaw), moved, and on the ground there (or as high above it as before). Pieces on the ground move
    /// as one block (see GroundBlock), those past the area's edge too, so a building across it keeps its shape. Anything
    /// else past the edge, where there is no ground, keeps its own height.
    /// </summary>
    /// <param name="m">The move.</param>
    /// <param name="it">The item.</param>
    /// <param name="block">Add the block's ground shift.</param>
    /// <param name="shift">Add the snap's shift.</param>
    private Vector3 Target(Move m, Item it, bool block = true, bool shift = true)
    {
        float t = m.Turn * D, c = MathF.Cos(t), sn = MathF.Sin(t), ox = it.Thing.Position.X - m.Cx, oz = it.Thing.Position.Z - m.Cz;
        float sx = shift ? m.Sx : 0, sy = shift ? m.Sy : 0, sz = shift ? m.Sz : 0;
        float x = m.Cx + ox * c + oz * sn + m.Dx + sx, z = m.Cz - ox * sn + oz * c + m.Dz + sz;
        if (it.Linked)
        {
            return new Vector3(x, it.Thing.Position.Y + m.Dy + sy, z);
        }
        if (it.Piece && OnGround && !m.Dropped)
        {
            return new Vector3(x, it.Thing.Position.Y + (block ? m.Block : 0) + m.Dy + sy, z);
        }
        if (GroundAt(x, z) is not float ground)
        {
            return new Vector3(x, it.Thing.Position.Y + m.Dy + sy, z);
        }
        return new Vector3(x, ground + (m.Dropped || !OnGround ? it.Lift : -it.Bottom) + m.Dy + sy, z);
    }
}
