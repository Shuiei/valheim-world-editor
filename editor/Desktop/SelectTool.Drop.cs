using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// End: each selected object drops from where it is onto whatever is under it: the top of another object (OnTop),
    /// or else the ground.
    /// </summary>
    public void Drop()
    {
        if (Begin() is not { } m)
        {
            return;
        }
        int onObjects = 0;
        // A linked group keeps its height (a ship stays afloat): its parts do not drop.
        var dropping = m.Items.Where(it => !it.Linked).ToList();
        if (dropping.Count == 0)
        {
            Message?.Invoke(Said.Of("select.msg.noDrop", Noun(m.Noun)), true);
            return;
        }
        foreach (var it in dropping)
        {
            var p = Target(m, it);
            // Past the area's edge there is no ground: only objects below, else it stays as high as it is.
            float ground = GroundAt(p.X, p.Z) ?? p.Y, y = ground;
            if (OnTop(m, it, p, ground) is float top)
            {
                y = top;
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
