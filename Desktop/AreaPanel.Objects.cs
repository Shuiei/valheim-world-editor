using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// The things inside the selection (indices), not deleted, of shown kinds (or added in this session).
    /// </summary>
    /// <param name="poly">The selection, in metres from the scene's corner.</param>
    /// <param name="chosenKindsOnly">Only the kinds toggled on, and only where the Mask lets the ground through.</param>
    /// <param name="includeHidden">Also kinds switched off in View (a backup restore compares with everything there).</param>
    /// <param name="maskAt">The Mask test to use (one the caller already took), or null to take it here.</param>
    internal List<int> ThingsInside(List<Vector2> poly, bool chosenKindsOnly, bool includeHidden = false, Func<float, float, bool>? maskAt = null)
    {
        var out_ = new List<int>();
        if (_view.Scene is not { } s)
        {
            return out_;
        }
        float ox = s.OriginX, oz = s.OriginZ;
        // Taken once: each Mask snapshot copies the whole ground.
        maskAt ??= chosenKindsOnly ? MaskTest() : null;
        lock (s.Things)
        {
            for (int i = 0; i < s.Things.Count; i++)
            {
                var t = s.Things[i];
                if (t.Gone || !AreaTool.Inside(poly, t.Position.X - ox, t.Position.Z - oz))
                {
                    continue;
                }
                var k = KindOf(t);
                if ((includeHidden || t.Id < 0 || _view.IsShown(k)) && (!chosenKindsOnly || Area.Kinds.Contains(k) && maskAt!(t.Position.X - ox, t.Position.Z - oz)))
                {
                    out_.Add(i);
                }
            }
        }
        return out_;
    }

    /// <summary>
    /// Whether the Mask lets the ground at a grid point through (the object actions pick only there). The Mask is
    /// taken now, once: each snapshot copies the whole ground, so take one per action, not one per object.
    /// </summary>
    /// <returns>A test by grid point (metres from the scene's corner); always true when the Mask is off.</returns>
    private Func<float, float, bool> MaskTest()
    {
        if (_session() is not { } s || s.MaskNow() is not { } m)
        {
            return static (_, _) => true;
        }
        int w = s.Ground.W, h = s.Ground.H;
        return (gx, gz) =>
        {
            int x = Math.Clamp((int)MathF.Round(gx), 0, w - 1), z = Math.Clamp((int)MathF.Round(gz), 0, h - 1);
            return m(z * w + x) > 0;
        };
    }

    /// <summary>A thing's object kind.</summary>
    private ObjectKind KindOf(WorldScene.Thing t) => ObjectKinds.Of(_nameOf(t.Prefab), t.Piece, t.Tamed);

    /// <summary>A prefab hash's name, or the hash itself when unknown.</summary>
    private string NameOf(int prefab) => _nameOf(prefab) ?? prefab.ToString();
}
