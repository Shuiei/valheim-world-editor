using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// End: each selected object drops onto whatever is under it: the top of another object, or else
    /// the ground.
    /// </summary>
    public void Drop()
    {
        if (Begin() is not { } m)
        {
            return;
        }
        var s = Scene!;
        var sel = m.Items.Select(it => it.Index).ToHashSet();
        int onObjects = 0;
        // A linked group keeps its height (a ship stays afloat): its parts do not drop.
        var dropping = m.Items.Where(it => !it.Linked).ToList();
        if (dropping.Count == 0)
        {
            Message?.Invoke(Said.Of("select.msg.noDrop", Noun(m.Noun)), true);
            return;
        }
        var under = DropIndex(s, sel);
        foreach (var it in dropping)
        {
            var p = Target(m, it);
            // The object's box (view space) brought to where the move puts it: the box was made where the
            // object was shown then, which the move may already have changed. Or a small box at its place.
            Vector3 lo, hi;
            if (_view.IsPickable(it.Index))
            {
                var (blo, bhi, at) = _view.BoxOf(it.Index);
                var shift = new Vector3(p.X - at.X, p.Y - at.Y, -(p.Z - at.Z));
                (lo, hi) = (blo + shift, bhi + shift);
            }
            else
            {
                (lo, hi) = (new Vector3(p.X - s.Cx - 0.1f, p.Y, -(p.Z - s.Cz) - 0.1f), new Vector3(p.X - s.Cx + 0.1f, p.Y + 0.2f, -(p.Z - s.Cz) + 0.1f));
            }
            float cx = (lo.X + hi.X) / 2, cz = (lo.Z + hi.Z) / 2, hx = (hi.X - lo.X) * 0.35f, hz = (hi.Z - lo.Z) * 0.35f;
            // Past the area's edge there is no ground: only objects below, else it stays as high as it is.
            float ground = GroundAt(p.X, p.Z) ?? p.Y, best = float.NegativeInfinity;
            // The highest top of another drawn object under the middle or four points of the footprint.
            foreach (var (ox, oz) in new[] { (0f, 0f), (-hx, -hz), (hx, -hz), (-hx, hz), (hx, hz) })
            {
                float x = cx + ox, z = cz + oz;
                foreach (var (bl, bh) in under.At(x, z))
                {
                    if (x >= bl.X && x <= bh.X && z >= bl.Z && z <= bh.Z && bh.Y <= hi.Y + 0.05f && bh.Y > best)
                    {
                        best = bh.Y;
                    }
                }
            }
            float y = best > ground + 0.01f ? best - (lo.Y - p.Y) : ground;
            if (best > ground + 0.01f)
            {
                onObjects++;
            }
            it.Lift = y - ground - m.Dy;
        }
        m.Dropped = true;
        Preview(m);
        Later();
        Message?.Invoke(Said.Of("select.msg.dropped", dropping.Count, onObjects, dropping.Count - onObjects), false);
    }
}
