
namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// How far the moved block goes up or down (Block): put on the ground, the pieces' block sits on what is under it
    /// (OnGroundBlock); not put on the ground, the whole selection follows the ground under its middle (FollowGround).
    /// Dropped with End, each item has its own height and there is no block.
    /// </summary>
    private void GroundBlock(Move m) => m.Block = m.Dropped ? 0 : OnGround ? OnGroundBlock(m) : FollowGround(m);

    /// <summary>
    /// Not put on the ground, the selection moves as one block, its shape kept: it goes up or down as much as the
    /// ground under its middle does, so it stays as high above the ground there as it was. 0 when its middle is, or
    /// was, past the area's edge, where there is no ground.
    /// </summary>
    private float FollowGround(Move m) =>
        GroundAt(m.Cx, m.Cz) is float from && GroundAt(m.Cx + m.Dx, m.Cz + m.Dz) is float to ? to - from : 0;

    /// <summary>
    /// Put on the ground, the selected building pieces keep their shape: the whole block goes up or down until its
    /// bottom layer sits on what is under it (Landed: a floor or a platform no higher than the block, else the ground),
    /// sinking into a slope or a platform's edge rather than floating. Pieces past the area's edge have no ground to
    /// sit on: the others decide, or none, and the block keeps its height (0). Pieces buried deep under the ground do
    /// not stand on it and go along with the block (see Standing).
    /// </summary>
    private float OnGroundBlock(Move m)
    {
        var pieces = Standing([.. m.Items.Where(it => it.Piece && !it.Linked)]);
        if (pieces.Count == 0)
        {
            return 0;
        }
        float low = pieces.Min(it => it.Thing.Position.Y + it.Bottom), need = float.PositiveInfinity;
        // What is under the bottom layer counts up to the block's top: a platform it is moved onto, not a tower beside it.
        float top = pieces.Max(it => BoxAt(it, it.Thing.Position).Hi.Y);
        foreach (var it in pieces.Where(it => it.Thing.Position.Y + it.Bottom <= low + 0.3f))
        {
            var p = Target(m, it, block: false, shift: false);
            if (GroundAt(p.X, p.Z) is float ground)
            {
                need = MathF.Min(need, Landed(m, it, p.X, p.Z, ground, top - it.Thing.Position.Y) - it.Thing.Position.Y);
            }
        }
        return float.IsFinite(need) ? need : 0;
    }

    /// <summary>
    /// The pieces that stand on the ground: all but those whose bottom was more than Picking.BuriedDepth under it (a
    /// mod's crafting stations hidden 10 km under a house), which would otherwise be the bottom layer and lift the
    /// whole building that far to put them on the ground. All of them when every one is that deep.
    /// </summary>
    /// <param name="pieces">The moved pieces.</param>
    private static List<Item> Standing(List<Item> pieces)
    {
        var standing = pieces.Where(it => it.Lift + it.Bottom > -Picking.BuriedDepth).ToList();
        return standing.Count > 0 ? standing : pieces;
    }

    /// <summary>The ground's height at a world point, as drawn; null outside the area, where there is no ground.</summary>
    private float? GroundAt(float wx, float wz)
    {
        var s = Scene!;
        float x = wx - s.Cx, z = -(wz - s.Cz);
        return Picking.Inside(s, x, z) ? Picking.HeightAt(s, x, z) : null;
    }
}
