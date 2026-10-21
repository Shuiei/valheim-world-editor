using System.Numerics;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Starts a dungeon's placement: the space taken around the point, the entrance the dungeon may stand at (its
    /// kind's, else its biome's), and the turn and door the entrance gives.
    /// </summary>
    /// <param name="scene">The area.</param>
    /// <param name="at">The point of the ground the entrance stands on.</param>
    /// <param name="camYaw">The camera's yaw, in radians.</param>
    /// <param name="d">The dungeon.</param>
    /// <returns>The placement, before its pieces are laid out.</returns>
    private static DungeonLay BeginLay(WorldScene scene, Vector3 at, float camYaw, DungeonToPlace d)
    {
        var world = scene.World;
        // What is taken high up: dungeons laid out, objects, and over the dungeons' locations near here, the space the
        // game will lay theirs out in.
        List<(int, Vector3)> high;
        lock (scene.Things)
        {
            high = [.. scene.Things.Where(t => !t.Gone && t.Position.Y > 3000).Select(t => (t.Prefab, t.Position))];
        }
        var near = world.Locations.Where(l => MathF.Abs(l.Position.X - at.X) < 600 && MathF.Abs(l.Position.Z - at.Z) < 600);
        var taken = DungeonGen.Taken(high, near);
        var source = world.SourceReader();
        // The entrance of its kind, when the game's files describe one with a door out; its front toward the camera.
        var kindOf = d.Rooms?.Invoke(null).Kind;
        var entrance = kindOf != null ? DungeonEntrances.For(kindOf.Name) : DungeonEntrances.ForBiome(d.Biome);
        var toward = at + new Vector3(MathF.Sin(camYaw), 0, -MathF.Cos(camYaw)) * 10;
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (entrance == null ? 0 : DungeonEntrances.YawToFace(entrance, at, toward)) * MathF.PI / 180f);
        var door = entrance == null ? null : DungeonEntrances.ExitPose(entrance, at, turn);
        var dungeonPose = entrance == null ? null : DungeonEntrances.DungeonPose(entrance, at, turn);
        return new DungeonLay
        {
            World = world,
            Source = source,
            Taken = taken,
            At = at,
            CamYaw = camYaw,
            Entrance = entrance,
            Turn = turn,
            Door = door,
            DungeonPose = dungeonPose,
            ViaEntrance = entrance != null && door != null && (d.Rooms == null || dungeonPose != null),
            Pieces = d.Pieces,
            // Where the dungeon's own frame stands in the world (its origin), and where a player arrives.
            Origin = new Vector3(MathF.Round(at.X), MathF.Round(at.Y) + 5000, MathF.Round(at.Z)),
            PortalYaw = d.Facing,
        };
    }

    /// <summary>
    /// Lays the dungeon's pieces out: the game's rooms from the dungeon object where the entrance puts it, or the
    /// pieces turned and moved so that the door out opens where a player arrives, or the pieces as they are.
    /// </summary>
    /// <param name="l">The placement.</param>
    /// <param name="d">The dungeon.</param>
    private static void LayOutPieces(DungeonLay l, DungeonToPlace d)
    {
        if (d.Rooms != null)
        {
            // The game's rooms: laid out from the dungeon object where the entrance puts it, else from the origin.
            var r = d.Rooms(l.ViaEntrance ? l.DungeonPose : null);
            l.Kind = r.Kind;
            l.Rooms = r.Rooms;
            l.Pieces = ToPieces(r.Items);
            l.Origin = l.ViaEntrance ? Vector3.Zero : DungeonGen.Clear(l.Origin, r, l.Taken);
            l.Arrival = l.Origin + r.Arrival;
            l.PortalYaw = 0f;
        }
        else if (l.ViaEntrance)
        {
            // The pieces turned and moved so that the door out opens where a player arrives, facing their way.
            var landing = DungeonEntrances.Landing(l.Door!.Value);
            var spin = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (DungeonEntrances.YawOf(l.Door.Value) - d.Facing) * MathF.PI / 180f);
            l.Pieces = [.. l.Pieces.Select(p => p with { Position = landing + Vector3.Transform(p.Position - d.Landing, spin), Rotation = Quaternion.Normalize(spin * p.Rotation) })];
            l.Origin = Vector3.Zero;
            l.Arrival = landing;
        }
        else
        {
            l.Origin = DungeonGen.Clear(l.Origin, [.. l.Pieces.Select(p => p.Position)], 10, l.Taken);
            l.Arrival = l.Origin + d.Landing;
        }
    }
}
