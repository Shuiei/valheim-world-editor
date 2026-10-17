using System.Numerics;
using TerrainEditor.Save;

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
    public sealed record Made(string Prefab, Vector3 Position, Quaternion Rotation);

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
    /// random parts' own heights are taken as the room's. The shared Unity random state is put back after.
    /// </summary>
    public static List<Made> Contents(Placed p, Kind kind, Vector3 dungeonPosition, int worldSeed)
    {
        if (p.Room is not { } room || room.Contents.Length == 0)
        {
            return [];
        }
        Vector3 v = kind.CustomInterior ? p.Position - dungeonPosition : p.Position;
        int seed = unchecked((int)v.X * 4271 + (int)v.Y * 9187 + (int)v.Z * 2134 + (kind.BaseSeed ? Seed(worldSeed, dungeonPosition) : 0));
        return Replay(room.Contents, room.Spawns, room.Picks, room.Nodes, kind.Themes, p.Position.Y, seed, p.Position, p.Rotation);
    }
}
