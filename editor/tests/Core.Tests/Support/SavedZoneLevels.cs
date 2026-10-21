using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// The levels a saved world holds, read back from the world's files on disk (not from the world the save was given).
/// </summary>
public static class SavedZoneLevels
{
    /// <summary>
    /// The level change at one point of a terrain zone, in the world saved in this folder.
    /// </summary>
    /// <param name="dir">The world's folder.</param>
    /// <param name="zone">A zone of the world before the save; the zone is found again by its coordinates.</param>
    /// <param name="point">The point's index in the zone's level change.</param>
    /// <returns>The level change at that point, in metres.</returns>
    public static float LevelDelta(string dir, TerrainZone zone, int point) =>
        WorldSave.Load(dir).TerrainZones.Single(q => q.ZoneX == zone.ZoneX && q.ZoneZ == zone.ZoneZ).LevelDelta[point];
}
