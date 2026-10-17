using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    /// <summary>An opening of a placed room, in world space.</summary>
    /// <param name="Room">The index of its room in the list.</param>
    /// <param name="Index">The index of the opening in its room's openings.</param>
    /// <param name="Position">Where it is.</param>
    /// <param name="Rotation">Which way it faces.</param>
    /// <param name="Type">Its connection type.</param>
    /// <param name="Entrance">The entrance room's way in.</param>
    public sealed record OpenEnd(int Room, int Index, Vector3 Position, Quaternion Rotation, string Type, bool Entrance);

    /// <summary>
    /// The openings of the placed rooms (unknown rooms have none); with <paramref name="freeOnly"/>, only
    /// those no other room meets (RoomConnection.TestContact: within 0.1 m), never the entrance's way in.
    /// </summary>
    public static List<OpenEnd> Openings(IReadOnlyList<Placed> rooms, bool freeOnly)
    {
        var all = new List<OpenEnd>();
        for (int i = 0; i < rooms.Count; i++)
        {
            Room? room = rooms[i].Room;
            if (room == null)
            {
                continue;
            }
            for (int k = 0; k < room.Openings.Length; k++)
            {
                var (p, q) = rooms[i].At(room.Openings[k]);
                all.Add(new OpenEnd(i, k, p, q, room.Openings[k].Type, room.Openings[k].Entrance));
            }
        }
        if (!freeOnly)
        {
            return all;
        }
        // The entrance room's way in leads out of the dungeon: it never takes a room.
        return [.. all.Where(a => !a.Entrance && !all.Any(b => b.Room != a.Room && Vector3.DistanceSquared(a.Position, b.Position) < 0.01f))];
    }

    // ---- Doors.
    /// <summary>
    /// Where two rooms meet, the game may put a door (DungeonGenerator.PlaceDoors): at the opening of the
    /// room that was there first, facing as it does, when that opening allows a door (and, OnlyIfOther, the
    /// new room's does too), and not where an end cap closes it.
    /// </summary>
    /// <param name="First">The index of the room that was there first.</param>
    /// <param name="Second">The index of the room that joined it.</param>
    /// <param name="Position">The first room's opening, in world space.</param>
    /// <param name="Rotation">Which way the first room's opening faces.</param>
    /// <param name="Type">The openings' connection type.</param>
    /// <param name="DoorAllowed">Whether a door may stand there.</param>
    public sealed record Joint(int First, int Second, Vector3 Position, Quaternion Rotation, string Type, bool DoorAllowed);

    /// <summary>Every place where two placed rooms' openings meet (within 0.1 m).</summary>
    public static List<Joint> Joints(IReadOnlyList<Placed> rooms)
    {
        var joints = new List<Joint>();
        var ends = Openings(rooms, freeOnly: false);
        foreach (var a in ends)
        {
            foreach (var b in ends)
            {
                if (b.Room <= a.Room || Vector3.DistanceSquared(a.Position, b.Position) >= 0.01f)
                {
                    continue;
                }
                var oa = rooms[a.Room].Room!.Openings[a.Index];
                var ob = rooms[b.Room].Room!.Openings[b.Index];
                bool allowed = !rooms[b.Room].Room!.EndCap && oa.AllowDoor && (!oa.OnlyIfOther || ob.AllowDoor);
                joints.Add(new Joint(a.Room, b.Room, a.Position, a.Rotation, a.Type, allowed));
            }
        }
        return joints;
    }
}
