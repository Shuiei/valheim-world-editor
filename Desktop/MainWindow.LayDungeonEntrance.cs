using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// The entrance's objects for a dungeon laid from it: its location proxy, which the game makes its door, its stones
    /// and its interior from, and the objects it makes, all but its own dungeon object.
    /// </summary>
    /// <param name="l">The placement.</param>
    /// <returns>False when the dungeon's space is taken (the message says so), else true.</returns>
    private bool AddEntranceObjects(DungeonLay l)
    {
        // The game lays one dungeon out per zone, in its box: another there already would share its space.
        var points = l.Pieces.Select(p => p.Position + l.Origin).Concat(l.Rooms?.Select(p => p.Position + l.Origin) ?? []).ToList();
        var box = points.Count == 0 ? new DungeonGen.Box(l.Arrival, l.Arrival) : new DungeonGen.Box(points.Aggregate(Vector3.Min) - new Vector3(10, 6, 10), points.Aggregate(Vector3.Max) + new Vector3(10, 6, 10));
        if (l.Taken.Any(t => t.Overlaps(box)))
        {
            Msg("dungeon.msg.spaceTaken");
            return false;
        }
        var entrance = l.Entrance!;
        var (zoneX, zoneZ) = ChunkMath.SectorOf(l.At.X, l.At.Z);
        int zoneSeed = LocationPrefabs.ZoneSeed(l.World.Seed, zoneX, zoneZ);
        ushort flags = Terrain.PrefabCatalog.Get(WorldSave.LocationProxyPrefab)?.Flags ?? 0x0B00;
        var euler = BlueprintFormats.ToEuler(l.Turn);
        l.Adds.Add((new NewObject(0, WorldSave.LocationProxyPrefab, l.At, euler, 0, Fresh: false,
            Raw: ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, flags, l.At, euler, 0f, [(WorldSave.LocationKey, entrance.Hash), (StableHash.Of("seed"), zoneSeed)])), false));
        foreach (var m in LocationPrefabs.Made(entrance, l.At, l.Turn, zoneSeed))
        {
            int prefab = StableHash.Of(m.Prefab);
            if (Dungeons.KindOf(prefab) == null && !m.Prefab.StartsWith("DG_", StringComparison.Ordinal) && l.Known(prefab))
            {
                l.Adds.Add((new NewObject(0, prefab, m.Position, BlueprintFormats.ToEuler(m.Rotation), 0), false));
            }
        }
        return true;
    }

    /// <summary>
    /// The game's rooms of a dungeon: a dungeon object holding the list, which the game builds them from, and the
    /// arrival on the entrance room's floor.
    /// </summary>
    /// <param name="l">The placement, with rooms and a kind.</param>
    /// <param name="d">The dungeon.</param>
    /// <returns>False when there are no rooms or the object cannot be made (the message says so), else true.</returns>
    private bool AddRoomsObject(DungeonLay l, DungeonToPlace d)
    {
        var kind = l.Kind!;
        var placed = l.Rooms!.Select(p => p with { Position = p.Position + l.Origin }).ToList();
        if (placed.Count == 0)
        {
            Msg("dungeon.msg.noRooms", d.Name);
            return false;
        }
        var pose = l.ViaEntrance ? l.DungeonPose!.Value : new DungeonEntrances.Pose(l.Origin, Quaternion.Identity);
        var dg = new NewObject(0, kind.Hash, pose.Position, BlueprintFormats.ToEuler(pose.Rotation), 0);
        if (l.Blank(dg) is not { } bytes)
        {
            Msg("dungeon.msg.cannotMake", kind.Name);
            return false;
        }
        var z = ZdoData.Parse(bytes);
        z.SetBytes(Dungeons.RoomDataKey, Dungeons.Write(placed));
        l.Adds.Add((dg with { Raw = z.Serialize(), Fresh = false }, false));
        l.DungeonAt = pose.Position;
        // Arriving on the entrance room's floor, found under its middle.
        var dungeon = new DungeonRooms.Dungeon(-1, new WorldScene.Thing(-1, kind.Hash, pose.Position, Vector3.Zero, 1, false), kind, placed, null);
        var above = placed[0].Position + new Vector3(0, 3, 0);
        l.Arrival = RoomSurfaces.Hit([dungeon], View.Models, above, -Vector3.UnitY) is float down ? above - new Vector3(0, down - 0.05f, 0) : placed[0].Position;
        return true;
    }
}
