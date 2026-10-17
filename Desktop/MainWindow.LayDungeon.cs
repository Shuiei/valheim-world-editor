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
        var world = scene.World;
        var (camYaw, _, _, _) = View.Camera;
        // What is taken high up: dungeons laid out, objects, and over the dungeons' locations near here, the space the
        // game will lay theirs out in.
        List<(int, Vector3)> high;
        lock (scene.Things)
        {
            high = [.. scene.Things.Where(t => !t.Gone && t.Position.Y > 3000).Select(t => (t.Prefab, t.Position))];
        }
        var near = world.Locations.Where(l => MathF.Abs(l.Position.X - at.X) < 600 && MathF.Abs(l.Position.Z - at.Z) < 600);
        var taken = DungeonGen.Taken(high, near);
        Func<ObjectRef, byte[]> source = world.SourceReader();
        byte[]? Blank(NewObject n) => world.NewObjectBytes(n, source);
        bool Known(int prefab) => CanMake(world, prefab);
        var adds = new List<(NewObject, bool)>();
        // The entrance of its kind, when the game's files describe one with a door out; its front toward the camera.
        var kindOf = d.Rooms?.Invoke(null).Kind;
        var entrance = kindOf != null ? DungeonEntrances.For(kindOf.Name) : DungeonEntrances.ForBiome(d.Biome);
        var toward = at + new Vector3(MathF.Sin(camYaw), 0, -MathF.Cos(camYaw)) * 10;
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (entrance == null ? 0 : DungeonEntrances.YawToFace(entrance, at, toward)) * MathF.PI / 180f);
        var door = entrance == null ? null : DungeonEntrances.ExitPose(entrance, at, turn);
        var dungeonPose = entrance == null ? null : DungeonEntrances.DungeonPose(entrance, at, turn);
        bool viaEntrance = entrance != null && door != null && (d.Rooms == null || dungeonPose != null);
        var pieces = d.Pieces;
        List<Dungeons.Placed>? rooms = null;
        Dungeons.Kind? kind = null;
        // Where the dungeon's own frame stands in the world (its origin), and where a player arrives.
        var origin = new Vector3(MathF.Round(at.X), MathF.Round(at.Y) + 5000, MathF.Round(at.Z));
        Vector3 arrival;
        float portalYaw = d.Facing;
        if (d.Rooms != null)
        {
            // The game's rooms: laid out from the dungeon object where the entrance puts it, else from the origin.
            var r = d.Rooms(viaEntrance ? dungeonPose : null);
            kind = r.Kind;
            rooms = r.Rooms;
            pieces = ToPieces(r.Items);
            origin = viaEntrance ? Vector3.Zero : DungeonGen.Clear(origin, r, taken);
            arrival = origin + r.Arrival;
            portalYaw = 0f;
        }
        else if (viaEntrance)
        {
            // The pieces turned and moved so that the door out opens where a player arrives, facing their way.
            var landing = DungeonEntrances.Landing(door!.Value);
            var spin = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (DungeonEntrances.YawOf(door.Value) - d.Facing) * MathF.PI / 180f);
            pieces = [.. pieces.Select(p => p with { Position = landing + Vector3.Transform(p.Position - d.Landing, spin), Rotation = Quaternion.Normalize(spin * p.Rotation) })];
            origin = Vector3.Zero;
            arrival = landing;
        }
        else
        {
            origin = DungeonGen.Clear(origin, [.. pieces.Select(p => p.Position)], 10, taken);
            arrival = origin + d.Landing;
        }
        Said way;
        if (viaEntrance)
        {
            // The game lays one dungeon out per zone, in its box: another there already would share its space.
            var points = pieces.Select(p => p.Position + origin).Concat(rooms?.Select(p => p.Position + origin) ?? []).ToList();
            var box = points.Count == 0 ? new DungeonGen.Box(arrival, arrival) : new DungeonGen.Box(points.Aggregate(Vector3.Min) - new Vector3(10, 6, 10), points.Aggregate(Vector3.Max) + new Vector3(10, 6, 10));
            if (taken.Any(t => t.Overlaps(box)))
            {
                Msg("dungeon.msg.spaceTaken");
                return;
            }
            // The entrance: its location proxy (the game makes its door, its stones and its interior from it whenever
            // the zone loads) and the objects it makes, all but its own dungeon object (the dungeon placed is the one).
            var (zoneX, zoneZ) = ChunkMath.SectorOf(at.X, at.Z);
            int zoneSeed = LocationPrefabs.ZoneSeed(world.Seed, zoneX, zoneZ);
            ushort flags = Terrain.PrefabCatalog.Get(WorldSave.LocationProxyPrefab)?.Flags ?? 0x0B00;
            var euler = BlueprintFormats.ToEuler(turn);
            adds.Add((new NewObject(0, WorldSave.LocationProxyPrefab, at, euler, 0, Fresh: false,
                Raw: ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, flags, at, euler, 0f, [(WorldSave.LocationKey, entrance!.Hash), (StableHash.Of("seed"), zoneSeed)])), false));
            foreach (var m in LocationPrefabs.Made(entrance, at, turn, zoneSeed))
            {
                int prefab = StableHash.Of(m.Prefab);
                if (Dungeons.KindOf(prefab) == null && !m.Prefab.StartsWith("DG_", StringComparison.Ordinal) && Known(prefab))
                {
                    adds.Add((new NewObject(0, prefab, m.Position, BlueprintFormats.ToEuler(m.Rotation), 0), false));
                }
            }
            way = Said.Of("dungeon.msg.entranceHere", DungeonEntrances.Describe(entrance));
        }
        else
        {
            way = Said.Of("dungeon.msg.portalHere", d.PortalTag);
        }
        Vector3? dungeonAt = null;
        if (rooms != null && kind != null)
        {
            // The game's rooms: a dungeon object holding the list, which the game builds them from.
            var placed = rooms.Select(p => p with { Position = p.Position + origin }).ToList();
            if (placed.Count == 0)
            {
                Msg("dungeon.msg.noRooms", d.Name);
                return;
            }
            var pose = viaEntrance ? dungeonPose!.Value : new DungeonEntrances.Pose(origin, Quaternion.Identity);
            var dg = new NewObject(0, kind.Hash, pose.Position, BlueprintFormats.ToEuler(pose.Rotation), 0);
            if (Blank(dg) is not { } bytes)
            {
                Msg("dungeon.msg.cannotMake", kind.Name);
                return;
            }
            var z = ZdoData.Parse(bytes);
            z.SetBytes(Dungeons.RoomDataKey, Dungeons.Write(placed));
            adds.Add((dg with { Raw = z.Serialize(), Fresh = false }, false));
            dungeonAt = pose.Position;
            // Arriving on the entrance room's floor, found under its middle.
            var dungeon = new DungeonRooms.Dungeon(-1, new WorldScene.Thing(-1, kind.Hash, pose.Position, Vector3.Zero, 1, false), kind, placed, null);
            var above = placed[0].Position + new Vector3(0, 3, 0);
            arrival = RoomSurfaces.Hit([dungeon], View.Models, above, -Vector3.UnitY) is float down ? above - new Vector3(0, down - 0.05f, 0) : placed[0].Position;
        }
        // Every object with its own data. Made fresh (nothing kept of the object it is copied from); the data goes on
        // when it is written (a generated dungeon's: no builder, so the game takes it for a ruin, as its own dungeons).
        int skipped = 0;
        foreach (var p in pieces)
        {
            if (!Known(p.Prefab))
            {
                skipped++;
                continue;
            }
            adds.Add((new NewObject(0, p.Prefab, p.Position + origin, BlueprintFormats.ToEuler(p.Rotation), p.Scale, Data: p.Data), p.Piece));
        }
        if (!viaEntrance)
        {
            // Two portals of the same tag, written linked (the game pairs only portals it sees made, or that
            // a save gives linked).
            int link = Random.Shared.Next(1, int.MaxValue);
            int portal = StableHash.Of("portal_wood");
            var portalData = new[] { ObjectField.NoBuilder, new ObjectField("strings", StableHash.Of("tag"), d.PortalTag) };
            foreach (var (where, yaw, kindByte) in new[] { (arrival, portalYaw, (byte)0x01), (GroundAt(scene, at.X, at.Z + 3), 180f, (byte)0x11) })
            {
                var n = new NewObject(0, portal, where, new Vector3(0, yaw, 0), 0);
                if (Blank(n) is not { } bytes)
                {
                    continue;
                }
                // The link is no field of the data: set on the object itself.
                var z = ZdoData.Parse(bytes);
                z.Connection = [kindByte, .. BitConverter.GetBytes(link)];
                adds.Add((n with { Raw = z.Serialize(), Fresh = false, Data = portalData }, true));
            }
        }
        session.Commit(d.Step, null, [], adds);
        LastPlaced = new PlacedDungeon(at, arrival, arrival.Y + 3.5f, dungeonAt);
        Msg(d.Done(adds.Count) + way
            + (skipped > 0 ? Said.Of("dungeon.msg.skipped", skipped) : Said.Empty) + Said.Of("dungeon.msg.goUp"));
        // The view stays on the ground, looking at the entrance.
        View.Orbit(at.X, at.Z, camYaw * 180f / MathF.PI, 55, 40);
        View.CutY = null;
        UpdateSaveBar();
    }
}
