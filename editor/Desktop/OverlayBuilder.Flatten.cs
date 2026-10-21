using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The overlay of the ground that locations flatten: the flat part, and the fainter blend into the land.
/// </summary>
internal sealed partial class OverlayBuilder
{
    /// <summary>
    /// Adds the ground that locations flatten: the flat part, and fainter where it blends into the land (not the
    /// editor's own ground discs for No limit ground: hundreds of them, they are just the ground).
    /// </summary>
    /// <param name="modifiers">The world's terrain modifiers; null draws none.</param>
    /// <returns>How many location ground modifiers (level or smooth) were drawn.</returns>
    public int AddFlatten(TerrainModifiers? modifiers)
    {
        int flattened = 0;
        if (modifiers != null)
        {
            var seen = new HashSet<TerrainModifiers.Modifier>();
            for (int zz = _s.Z0; zz < _s.Z0 + _s.ZonesZ; zz++)
            {
                for (int zx = _s.X0; zx < _s.X0 + _s.ZonesX; zx++)
                {
                    foreach (var m in modifiers.InZone(zx, zz).Where(m => !m.Player && !m.Disc && (m.Level || m.Smooth)))
                    {
                        if (!seen.Add(m))
                        {
                            continue;
                        }
                        flattened++;
                        if (m.Level && m.LevelRadius > 0)
                        {
                            Ring(Overlays.Layer.Flatten, m.Position.X, m.Position.Z, m.LevelRadius, m.Square);
                        }
                        if (m.Smooth && m.SmoothRadius > 0)
                        {
                            Ring(Overlays.Layer.FlattenEdge, m.Position.X, m.Position.Z, m.SmoothRadius, m.Square);
                        }
                    }
                }
            }
        }
        return flattened;
    }
}
