using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// A dungeon into the world, 5000 m above a point of the ground (the game builds dungeons there, and treats what is
    /// above 3000 m as indoors). On the ground there, facing the camera, the entrance of its kind (the dungeon location
    /// of its biome, or of its own kind of game dungeon: a burial chamber's mound, a Frost Cave's mouth...), whose door
    /// leads up into it and whose door up there leads back out: the dungeon is laid out where the game lays that
    /// entrance's own, turned so that a player coming through the door out arrives where the dungeon has them arrive,
    /// facing its way. When the game's files describe no such entrance, a portal on the ground and its twin where the
    /// player arrives, linked. The view stays at the entrance (<see cref="GoToDungeon"/> goes up,
    /// <see cref="GoToEntrance"/> comes back). One step of the history: Ctrl+Z takes it all back.
    /// </summary>
    /// <param name="scene">The area.</param>
    /// <param name="session">Its edits.</param>
    /// <param name="at">The point of the ground the entrance stands on.</param>
    /// <param name="d">The dungeon.</param>
    private void PlaceDungeon(WorldScene scene, EditSession session, Vector3 at, DungeonToPlace d)
    {
        try
        {
            LayDungeon(scene, session, at, d);
        }
        // The objects' data is read from the world's files (NewObjectBytes): one gone or locked meanwhile places nothing.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Caught(ex, "placing a dungeon");
            Msg("dungeon.msg.notPlaced", d.Name, ex.Message);
        }
    }
}
