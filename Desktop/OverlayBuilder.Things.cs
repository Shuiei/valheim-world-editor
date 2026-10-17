using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The overlays of the things and the ground modifiers: the ward and crafting station rings, and the ground that
/// locations flatten.
/// </summary>
internal sealed partial class OverlayBuilder
{
    /// <summary>Adds a ring for each ward and each crafting station the scene's things have.</summary>
    /// <param name="names">Each thing's prefab name by index in the scene's things (null when unknown).</param>
    /// <returns>How many wards were ringed, and how many crafting stations.</returns>
    public (int Wards, int Stations) AddWardsAndStations(Func<int, string?> names)
    {
        var ranges = PrefabCatalog.Ranges;
        int wards = 0, stations = 0;
        for (int i = 0; i < _s.Things.Count; i++)
        {
            if (names(i) is not string n || !ranges.TryGetValue(n, out var r))
            {
                continue;
            }
            var t = _s.Things[i];
            if (t.Gone)
            {
                continue;
            }
            if (r.WardRadius > 0)
            {
                Ring(Overlays.Layer.Wards, t.Position.X, t.Position.Z, r.WardRadius);
                wards++;
            }
            if (r.BuildRange > 0)
            {
                Ring(Overlays.Layer.Stations, t.Position.X, t.Position.Z, r.BuildRange);
                stations++;
            }
        }
        return (wards, stations);
    }
}
