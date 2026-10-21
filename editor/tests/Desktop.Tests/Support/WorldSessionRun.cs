using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The arrange steps the live world tests share: a stand-in game's live world, its first zone as an edit session, and
/// that zone raised or lifted.
/// </summary>
internal static class WorldSessionRun
{
    /// <summary>
    /// Opens a live world on the stand-in game.
    /// </summary>
    internal static Task<WorldSession> OpenLive(FakeGame game) => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());

    /// <summary>
    /// The world's first zone as an edit session.
    /// </summary>
    internal static EditSession Zone(WorldSession world) => WorldScene.Load(world, 0, 0, 1).Session!;

    /// <summary>
    /// The world's first zone as an edit session, with a raise of 3 at grid point (x, z) by the formula "2".
    /// </summary>
    internal static EditSession RaisedZone(WorldSession world, int x, int z)
    {
        var s = Zone(world);
        s.Shape(x, z, Formula.Compile("2", []), 3, 0, "raise");
        return s;
    }

    /// <summary>
    /// Lifts every point of the world's first zone by these metres (as No limit ground does), and keeps the zone.
    /// </summary>
    internal static void LiftZone(WorldSession world, float metres)
    {
        var lifted = world.Edits.Get(0, 0) ?? new ZoneEdit(0, 0);
        for (int i = 0; i < EditStore.Cells; i++)
        {
            lifted.Lift[i] = metres;
        }
        world.Edits.Put(lifted);
    }

    /// <summary>
    /// Deletes the first object of the area that is not the game's and not gone.
    /// </summary>
    internal static void DeleteFirstObject(EditSession s) => s.Delete([s.Scene.Things.FindIndex(t => t.Id >= 0 && !t.Gone)]);
}
