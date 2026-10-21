using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// Where an object's origin lands when it is put on the ground at (x, z): carried there at the height above the
    /// ground it had (Lift) and let down as End drops it, onto the highest drawn top under it (OnTop: a floor, a table,
    /// a rock), or, when there is none, onto the ground, with its bottom on it (Bottom: a piece's lowest snap point
    /// below its origin, 0 for anything else, whose origin is its foot).
    /// </summary>
    /// <param name="m">The move.</param>
    /// <param name="it">The item.</param>
    /// <param name="x">World x where it ends up.</param>
    /// <param name="z">World z where it ends up.</param>
    /// <param name="ground">The ground's height there.</param>
    /// <param name="above">
    /// How far above the object's origin a top under it may reach and still count (a block of pieces: the block's top);
    /// null for the object's own top.
    /// </param>
    private float Landed(Move m, Item it, float x, float z, float ground, float? above = null)
    {
        var carried = new Vector3(x, ground + it.Lift, z);
        return OnTop(m, it, carried, ground, above is float a ? carried.Y + a : null) ?? ground - it.Bottom;
    }

    /// <summary>
    /// The height an object's origin is at with its box's bottom on the highest drawn top under it (Under); null when
    /// nothing but the ground is under it.
    /// </summary>
    /// <param name="m">The move.</param>
    /// <param name="it">The item.</param>
    /// <param name="p">Where the object is, world coordinates.</param>
    /// <param name="ground">The ground's height there (the object's own height where there is none).</param>
    /// <param name="ceiling">The highest a top may be and count; null for the object's own top.</param>
    private float? OnTop(Move m, Item it, Vector3 p, float ground, float? ceiling = null)
    {
        var (lo, hi) = BoxAt(it, p);
        return Under(m, lo, hi, ceiling ?? hi.Y, ground) is float top ? top - (lo.Y - p.Y) : null;
    }

    /// <summary>
    /// The highest top of another drawn object (not a moved one) under an object's box: a top under the middle or one
    /// of the four corners of its footprint, above the ground and no higher than the ceiling (5 cm over it allowed: the
    /// object may sit a little in a floor). Null when nothing but the ground is under it. A roof or a wall taller than
    /// the ceiling does not count: the object goes under it.
    /// </summary>
    /// <param name="m">The move (the boxes of the objects around it are gathered at its first call).</param>
    /// <param name="lo">The object's box, its low corner (view space).</param>
    /// <param name="hi">The object's box, its high corner (view space).</param>
    /// <param name="ceiling">The highest a top may be and count.</param>
    /// <param name="ground">The ground's height there.</param>
    private float? Under(Move m, Vector3 lo, Vector3 hi, float ceiling, float ground)
    {
        m.Under ??= DropIndex(Scene!, [.. m.Items.Select(it => it.Index)]);
        float cx = (lo.X + hi.X) / 2, cz = (lo.Z + hi.Z) / 2, hx = (hi.X - lo.X) * 0.35f, hz = (hi.Z - lo.Z) * 0.35f;
        float best = float.NegativeInfinity;
        foreach (var (ox, oz) in new[] { (0f, 0f), (-hx, -hz), (hx, -hz), (-hx, hz), (hx, hz) })
        {
            float x = cx + ox, z = cz + oz;
            foreach (var (bl, bh) in m.Under.At(x, z))
            {
                if (x >= bl.X && x <= bh.X && z >= bl.Z && z <= bh.Z && bh.Y <= ceiling + 0.05f && bh.Y > best)
                {
                    best = bh.Y;
                }
            }
        }
        return best > ground + 0.01f ? best : null;
    }

    /// <summary>
    /// An object's box (view space) brought to where the move puts it: the box was made where the object was shown
    /// then, which the move may already have changed. Or a small box at its place, when it is not drawn.
    /// </summary>
    /// <param name="it">The item.</param>
    /// <param name="p">Where the object is, world coordinates.</param>
    private (Vector3 Lo, Vector3 Hi) BoxAt(Item it, Vector3 p)
    {
        var s = Scene!;
        if (_view.IsPickable(it.Index))
        {
            var (blo, bhi, at) = _view.BoxOf(it.Index);
            var shift = new Vector3(p.X - at.X, p.Y - at.Y, -(p.Z - at.Z));
            return (blo + shift, bhi + shift);
        }
        return (new Vector3(p.X - s.Cx - 0.1f, p.Y, -(p.Z - s.Cz) - 0.1f), new Vector3(p.X - s.Cx + 0.1f, p.Y + 0.2f, -(p.Z - s.Cz) + 0.1f));
    }
}
