using System.Numerics;
using TerrainEditor.Save;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    /// <summary>
    /// Whether a room stays inside the dungeon's box (DungeonGenerator.IsInsideDungeon: all eight corners
    /// of its box within m_zoneSize around the zone's centre, at the dungeon's height). An unknown room does.
    /// </summary>
    public static bool Inside(Kind kind, Vector3 dungeonPosition, Placed p)
    {
        if (p.Room is not { } room)
        {
            return true;
        }
        const float Zone = 64f;
        var center = new Vector3(MathF.Floor((dungeonPosition.X + Zone / 2) / Zone) * Zone, dungeonPosition.Y, MathF.Floor((dungeonPosition.Z + Zone / 2) / Zone) * Zone);
        Vector3 min = center - kind.ZoneSize / 2, max = center + kind.ZoneSize / 2;
        Vector3 h = room.Size / 2;
        foreach (float sx in new[] { -1f, 1f })
        {
            foreach (float sy in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 c = p.ToWorld(new Vector3(sx * h.X, sy * h.Y, sz * h.Z));
                    if (c.X < min.X || c.Y < min.Y || c.Z < min.Z || c.X > max.X || c.Y > max.Y || c.Z > max.Z)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    // ---- What the game makes with a room when it generates it.
    /// <summary>An object the game makes with a room, in world space.</summary>
    /// <param name="Prefab">The object's prefab name.</param>
    /// <param name="Position">World space.</param>
    /// <param name="Rotation">World space.</param>
    /// <param name="Snap">The height above the ground its SnapToGround puts it at; null for none.</param>
    /// <param name="Data">The data Expand World Data's files give it; null for none.</param>
    public sealed record Made(string Prefab, Vector3 Position, Quaternion Rotation, float? Snap = null, IReadOnlyList<ValheimGen.ContentField>? Data = null);

    /// <summary>The dungeon's seed (DungeonGenerator.GetSeed): the world's seed and the dungeon's zone and place.</summary>
    public static int Seed(int worldSeed, Vector3 dungeonPosition)
    {
        var (zx, zz) = ChunkMath.SectorOf(dungeonPosition.X, dungeonPosition.Z);
        return unchecked(worldSeed + zx * 4271 + zz * -7187 + (int)dungeonPosition.X * -4271 + (int)dungeonPosition.Y * 9187 + (int)dungeonPosition.Z * -2134);
    }

    /// <summary>
    /// What the game makes with a room when it generates it (DungeonGenerator.PlaceRoom, SpawnMode Full): its
    /// networked objects, those its random parts keep, rolled from the room's place the game's way
    /// (Random.InitState, then each RandomSpawn's chance, then each RandomObject's pick, in order). The
    /// random parts' own heights are taken as the room's. With Expand World Data's files, each is then spawned as the mod
    /// spawns it (<see cref="ModSpawn"/>), drawing on from the rolls, and the room file's objects are added. The shared
    /// Unity random state is put back after.
    /// </summary>
    public static List<Made> Contents(Placed p, Kind kind, Vector3 dungeonPosition, int worldSeed)
    {
        if (p.Room is not { } room)
        {
            return [];
        }
        List<Made> made = [];
        if (room.Contents.Length > 0)
        {
            Vector3 v = kind.CustomInterior ? p.Position - dungeonPosition : p.Position;
            int seed = unchecked((int)v.X * 4271 + (int)v.Y * 9187 + (int)v.Z * 2134 + (kind.BaseSeed ? Seed(worldSeed, dungeonPosition) : 0));
            made = Spawned(Replay(room.Contents, room.Spawns, room.Picks, room.Nodes, kind.Themes, p.Position.Y, seed, p.Position, p.Rotation, out var rolled), room, kind, rolled);
        }
        made.AddRange(BlueprintMade(room, kind, p));
        made.AddRange(Added(room, kind, p));
        return made;
    }

    /// <summary>The seed Expand World Data draws a room's added objects from: the room's place (its PlaceRoom postfix).</summary>
    private static int PlaceSeed(Vector3 at) => unchecked((int)at.X * 4271 + (int)at.Y * 9187 + (int)at.Z * 2134);

    /// <summary>
    /// The parts of a blueprint room's blueprint (Expand World Data's SpawnBlueprintRoom: Create.Blueprint at the room's
    /// place and turn): each by its chance, a blueprint it nests spawning its own parts in its place, each spawned as the
    /// room's objects are (<see cref="ModSpawn"/>). The game draws their chances from the dungeon generator's numbers,
    /// which the Dungeon tool does not follow: they are drawn from the room's place here.
    /// </summary>
    private static List<Made> BlueprintMade(Room room, Kind kind, Placed p)
    {
        if (room.Blueprint is not { } bp)
        {
            return [];
        }
        List<Made?> made = [];
        var was = Rnd.State;
        try
        {
            Rnd.InitState(PlaceSeed(p.Position));
            AddParts(made, new PartsOf(bp, room, kind), bp.Name, p.Position, p.Rotation);
        }
        finally
        {
            Rnd.State = was;
        }
        return [.. made.OfType<Made>()];
    }

    /// <summary>The blueprint a blueprint room's parts come from, with the room and dungeon they are spawned for.</summary>
    /// <param name="Blueprint">The blueprint.</param>
    /// <param name="Room">The room.</param>
    /// <param name="Kind">The dungeon kind.</param>
    private readonly record struct PartsOf(ValheimGen.RoomBlueprint Blueprint, Room Room, Kind Kind);

    /// <summary>One blueprint's parts at a place and turn (see <see cref="BlueprintMade"/>).</summary>
    private static void AddParts(List<Made?> made, PartsOf of, string name, Vector3 at, Quaternion turn)
    {
        foreach (var part in of.Blueprint.Blueprints[name])
        {
            if (part.Chance < 1f && Rnd.Value > part.Chance)
            {
                continue;
            }
            var position = at + Vector3.Transform(part.Position, turn);
            var rotation = Quaternion.Normalize(turn * part.Rotation);
            if (part.Prefab != name && of.Blueprint.Blueprints.ContainsKey(part.Prefab))
            {
                AddParts(made, of, part.Prefab, position, rotation);
                continue;
            }
            made.Add(ModSpawn(new Made(part.Prefab, position, rotation), part.Data, of.Room, of.Kind));
        }
    }

    /// <summary>
    /// The room's own objects spawned as the mod spawns them, in order, drawing from the rolls' random state; as they are
    /// when neither file changes them.
    /// </summary>
    private static List<Made> Spawned(List<Made> made, Room room, Kind kind, Rnd.RandomState rolled)
    {
        if (!kind.Changes.Any && !room.Changes.Any)
        {
            return made;
        }
        var was = Rnd.State;
        try
        {
            Rnd.State = rolled;
            return [.. made.Select(m => ModSpawn(m, null, room, kind)).OfType<Made>()];
        }
        finally
        {
            Rnd.State = was;
        }
    }

    /// <summary>
    /// One object spawned as Expand World Data spawns a room's (its CustomObject, Create.BPO): swapped by the dungeon
    /// file's swaps, then the room file's (a draw each when there are several choices); given the dungeon's data, then the
    /// room's, then its own; then it wakes (its scripts' draws, <see cref="Terrain.WakeDraws"/>). Null when a swap removes
    /// it or turns it into what the editor does not know (a blueprint is not built); a swapped object no longer snaps.
    /// </summary>
    private static Made? ModSpawn(Made m, IReadOnlyList<ValheimGen.ContentField>? own, Room room, Kind kind)
    {
        string name = ValheimGen.ModObjects.Swapped(ValheimGen.ModObjects.Swapped(m.Prefab, kind.Changes.Swaps), room.Changes.Swaps);
        if (name == "" || name != m.Prefab && Terrain.PrefabCatalog.Get(StableHash.Of(name)) == null)
        {
            return null;
        }
        List<ValheimGen.ContentField> fields = [];
        ValheimGen.ModObjects.AddData(fields, kind.Changes.Data, name);
        ValheimGen.ModObjects.AddData(fields, room.Changes.Data, name);
        fields.AddRange(own ?? []);
        Terrain.WakeDraws.Wake(name, new(() => Terrain.WakeDraws.Skip(2), Looted: Terrain.WakeDraws.Looted(fields)));
        return m with { Prefab = name, Snap = name == m.Prefab ? m.Snap : null, Data = fields.Count > 0 ? fields : null };
    }

    /// <summary>
    /// The objects Expand World Data's room file adds with a room (its PlaceRoom postfix): each by its chance, drawn from
    /// the room's place (the same roll whatever the dungeon), at its place in the room's frame and turned with it, then
    /// spawned as the room's own objects are (<see cref="ModSpawn"/>, its own data last).
    /// </summary>
    private static List<Made> Added(Room room, Kind kind, Placed p)
    {
        if (room.Added.Count == 0)
        {
            return [];
        }
        List<Made?> made = [];
        var was = Rnd.State;
        try
        {
            Rnd.InitState(PlaceSeed(p.Position));
            foreach (var o in room.Added)
            {
                if (o.Chance < 1f && Rnd.Value > o.Chance)
                {
                    continue;
                }
                made.Add(ModSpawn(new Made(o.Prefab, p.ToWorld(o.Position), Quaternion.Normalize(p.Rotation * App.BlueprintFormats.FromEuler(o.Euler))), o.Data, room, kind));
            }
        }
        finally
        {
            Rnd.State = was;
        }
        return [.. made.OfType<Made>()];
    }
}
