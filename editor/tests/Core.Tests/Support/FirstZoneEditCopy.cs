using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// A copy of a world's ground edit, to change in a test without touching the edit the world holds.
/// </summary>
public static class FirstZoneEditCopy
{
    /// <summary>
    /// A copy of the ground edit of the world's first terrain zone.
    /// </summary>
    public static ZoneEdit Of(WorldSave world)
    {
        var t = world.TerrainZones[0];
        return new EditStore(world).Get(t.ZoneX, t.ZoneZ)!.Clone();
    }
}
