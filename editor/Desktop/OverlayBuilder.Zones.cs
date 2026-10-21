using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// The zone borders and the location posts of the overlays.
/// </summary>
internal sealed partial class OverlayBuilder
{
    /// <summary>Adds the zone borders: a point every 2 m along each zone edge, just above the ground.</summary>
    public void AddZoneBorders()
    {
        for (int i = 0; i <= Math.Max(_s.ZonesX, _s.ZonesZ); i++)
        {
            foreach (bool vertical in new[] { true, false })
            {
                if (i > (vertical ? _s.ZonesX : _s.ZonesZ))
                {
                    continue;
                }
                var pts = new List<Vector3>();
                for (int t = 0; t < (vertical ? _s.H : _s.W); t += 2)
                {
                    int gx = vertical ? i * 64 : t, gz = vertical ? t : i * 64;
                    if (gx < _s.W && gz < _s.H)
                    {
                        pts.Add(OnGround(gx, gz, 0.15f));
                    }
                }
                Strip(Overlays.Layer.Borders, pts);
            }
        }
    }

    /// <summary>Adds a 14 m post at each location in or near the area.</summary>
    /// <returns>How many locations got a marker.</returns>
    public int AddLocationMarkers()
    {
        int locations = 0;
        foreach (var (p, _) in Overlays.RealLocations(_s))
        {
            if (!Overlays.Near(_s, p))
            {
                continue;
            }
            locations++;
            var b = OnGround(p.X - _ox, p.Z - _oz, 0);
            foreach (var (dx, dz) in new[] { (0.8f, 0f), (-0.8f, 0f), (0f, 0.8f), (0f, -0.8f), (0f, 0f) })
            {
                _lines[Overlays.Layer.Markers].AddRange([b.X + dx, b.Y, b.Z + dz, b.X + dx, b.Y + 14, b.Z + dz]);
            }
        }
        return locations;
    }
}
