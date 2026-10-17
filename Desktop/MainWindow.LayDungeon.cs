using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary><see cref="PlaceDungeon"/>'s work; the world's files read failing throws.</summary>
    /// <param name="scene">The area.</param>
    /// <param name="session">Its edits.</param>
    /// <param name="at">The point of the ground the entrance stands on.</param>
    /// <param name="d">The dungeon.</param>
    private void LayDungeon(WorldScene scene, EditSession session, Vector3 at, DungeonToPlace d)
    {
        var (camYaw, _, _, _) = View.Camera;
        var lay = BeginLay(scene, at, camYaw, d);
        LayOutPieces(lay, d);
        if (lay.ViaEntrance && !AddEntranceObjects(lay))
        {
            return;
        }
        if (lay.Rooms != null && lay.Kind != null && !AddRoomsObject(lay, d))
        {
            return;
        }
        Said way = lay.ViaEntrance ? Said.Of("dungeon.msg.entranceHere", DungeonEntrances.Describe(lay.Entrance!)) : Said.Of("dungeon.msg.portalHere", d.PortalTag);
        AddPieces(lay);
        if (!lay.ViaEntrance)
        {
            AddPortalPair(scene, lay, d);
        }
        session.Commit(d.Step, null, [], lay.Adds);
        LastPlaced = new PlacedDungeon(at, lay.Arrival, lay.Arrival.Y + 3.5f, lay.DungeonAt);
        Msg(d.Done(lay.Adds.Count) + way
            + (lay.Skipped > 0 ? Said.Of("dungeon.msg.skipped", lay.Skipped) : Said.Empty) + Said.Of("dungeon.msg.goUp"));
        // The view stays on the ground, looking at the entrance.
        View.Orbit(at.X, at.Z, camYaw * 180f / MathF.PI, 55, 40);
        View.CutY = null;
        UpdateSaveBar();
    }

    /// <summary>
    /// The placement of one dungeon before its objects are added: the space it takes, the entrance it may stand at,
    /// and where its origin and arrival are.
    /// </summary>
    private sealed class DungeonLay
    {
        /// <summary>The world the dungeon goes in.</summary>
        public required WorldSave World { get; init; }

        /// <summary>The reader of the bytes the new objects are copied from.</summary>
        public required Func<ObjectRef, byte[]> Source { get; init; }

        /// <summary>The space already taken around the point.</summary>
        public required List<DungeonGen.Box> Taken { get; init; }

        /// <summary>The point of the ground the entrance stands on.</summary>
        public required Vector3 At { get; init; }

        /// <summary>The camera's yaw, in radians.</summary>
        public required float CamYaw { get; init; }

        /// <summary>The entrance of the dungeon's kind, when the game's files describe one with a door out.</summary>
        public required LocationPrefabs.Prefab? Entrance { get; init; }

        /// <summary>The turn the entrance is put at.</summary>
        public required Quaternion Turn { get; init; }

        /// <summary>Where the door out of the entrance stands, when there is one.</summary>
        public required DungeonEntrances.Pose? Door { get; init; }

        /// <summary>Where the dungeon's own object stands when it is laid from the entrance.</summary>
        public required DungeonEntrances.Pose? DungeonPose { get; init; }

        /// <summary>Whether the dungeon is laid from its entrance.</summary>
        public required bool ViaEntrance { get; init; }

        /// <summary>The dungeon's pieces, as they are laid.</summary>
        public required List<DungeonPiece> Pieces { get; set; }

        /// <summary>The game's rooms, when the dungeon has them.</summary>
        public List<Dungeons.Placed>? Rooms { get; set; }

        /// <summary>The game's kind of the dungeon, when it has rooms.</summary>
        public Dungeons.Kind? Kind { get; set; }

        /// <summary>Where the dungeon's own frame stands in the world.</summary>
        public required Vector3 Origin { get; set; }

        /// <summary>Where a player arrives.</summary>
        public Vector3 Arrival { get; set; }

        /// <summary>The yaw the portals are turned to, in degrees.</summary>
        public required float PortalYaw { get; set; }

        /// <summary>The objects to add, and whether each is a piece.</summary>
        public List<(NewObject, bool)> Adds { get; } = [];

        /// <summary>Where the dungeon object stands, when the dungeon has one.</summary>
        public Vector3? DungeonAt { get; set; }

        /// <summary>The pieces the game does not know how to make.</summary>
        public int Skipped { get; set; }

        /// <summary>An object's bytes as the world makes them, or null when it cannot be made.</summary>
        /// <param name="n">The new object.</param>
        public byte[]? Blank(NewObject n) => World.NewObjectBytes(n, Source);

        /// <summary>Whether the game can make a prefab here.</summary>
        /// <param name="prefab">The prefab's hash.</param>
        public bool Known(int prefab) => CanMake(World, prefab);
    }
}
