
namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// On the ground, the selected building pieces keep their shape: the whole block goes up or down
    /// until its bottom layer sits on the ground (sinking into a slope rather than floating). Pieces past the area's
    /// edge have no ground to sit on: the others decide, or none, and the block keeps its height.
    /// </summary>
    private void GroundBlock(Move m)
    {
        m.Block = 0;
        var pieces = m.Items.Where(it => it.Piece && !it.Linked).ToList();
        if (pieces.Count == 0 || !OnGround || m.Dropped)
        {
            return;
        }
        float low = pieces.Min(it => it.Thing.Position.Y + it.Bottom), need = float.PositiveInfinity;
        foreach (var it in pieces.Where(it => it.Thing.Position.Y + it.Bottom <= low + 0.3f))
        {
            var p = Target(m, it, block: false, shift: false);
            if (GroundAt(p.X, p.Z) is float ground)
            {
                need = MathF.Min(need, ground - (it.Thing.Position.Y + it.Bottom));
            }
        }
        if (float.IsFinite(need))
        {
            m.Block = need;
        }
    }

    /// <summary>The ground's height at a world point, as drawn; null outside the area, where there is no ground.</summary>
    private float? GroundAt(float wx, float wz)
    {
        var s = Scene!;
        float x = wx - s.Cx, z = -(wz - s.Cz);
        return Picking.Inside(s, x, z) ? Picking.HeightAt(s, x, z) : null;
    }
}
