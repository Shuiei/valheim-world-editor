using System.Numerics;

namespace TerrainEditor.Desktop;

// The objects standing in the scene, as the spacing check needs them.
public sealed partial class PlaceTool
{
    /// <summary>Every object standing in the block (grid positions), for the spacing check.</summary>
    private static Hash Standing(WorldScene s, float cell)
    {
        var h = new Hash(cell);
        float ox = s.OriginX, oz = s.OriginZ;
        lock (s.Things)
        {
            foreach (var t in s.Things)
            {
                if (!t.Gone)
                {
                    h.Add(new Vector2(t.Position.X - ox, t.Position.Z - oz));
                }
            }
        }
        return h;
    }
}
