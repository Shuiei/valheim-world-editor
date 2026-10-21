using System.Numerics;
using ValheimGen;
using NVector3 = System.Numerics.Vector3;

namespace TerrainEditor.App;

// Expand World Data's blueprint rooms: a room of the room file built of a blueprint (RoomManager.CreateRoomData,
// BlueprintManager.Load with the room's connections, RoomSpawning.SetupAvailableRooms).
public static partial class ExpandWorld
{
    /// <summary>
    /// A blueprint room's blueprint as the mod loads it for the room: its file's snap points, each over-written by the
    /// room's connection of the same place: a position of three numbers ("x, z, y") a snap point there, unturned; a
    /// piece's name the first such piece still made (it is then not made: its place and turn the snap point's). The
    /// snap points stay where the file has them (the mod does not centre them with the pieces). Its size is its pieces'
    /// bounds, the origin among them. Null when there is no blueprint of that name or it cannot be read (noted).
    /// </summary>
    /// <param name="folder">The folder the mod reads blueprint files from; null for none.</param>
    /// <param name="name">The blueprint's name (the room's name before any ":").</param>
    /// <param name="snapPieces">The room's connections' positions, in order.</param>
    /// <param name="data">The world's data entries (what the parts' data names).</param>
    /// <param name="why">Where what cannot be made is said.</param>
    /// <param name="notes">Where a file that cannot be read is noted.</param>
    internal static RoomBlueprint? RoomBlueprintOf(string? folder, string name, IReadOnlyList<string> snapPieces, DataTable data, List<string> why, List<string> notes)
    {
        string[] files = BlueprintFiles(folder);
        if (ReadRaw(files, name, data, why, notes) is not { } raw)
        {
            return null;
        }
        var snaps = raw.SnapPoints.Select(p => (p, Quaternion.Identity)).ToList();
        for (int i = 0; i < snapPieces.Count; i++)
        {
            if (SnapFrom(raw.Parts, snapPieces[i]) is { } snap)
            {
                SetSnap(snaps, snap, i);
            }
        }
        var min = raw.Parts.Aggregate(NVector3.Zero, (m, p) => NVector3.Min(m, p.Position));
        var max = raw.Parts.Aggregate(NVector3.Zero, (m, p) => NVector3.Max(m, p.Position));
        var blueprints = BlueprintsOf(folder, name, data, why, notes) ?? [];
        blueprints[name] = Centred(raw).Parts;
        return new RoomBlueprint(name, blueprints, snaps, max - min);
    }

    /// <summary>
    /// The snap point a connection's position gives (Blueprint.LoadSnapPoints): three numbers a place, unturned; else the
    /// first piece of that name still made, which then is not; none when there is no such piece.
    /// </summary>
    private static (NVector3, Quaternion)? SnapFrom(List<BlueprintPart> parts, string piece)
    {
        string[] numbers = piece.Split(',');
        if (numbers.Length == 3)
        {
            return (new NVector3(Num(numbers, 0), Num(numbers, 2), Num(numbers, 1)), Quaternion.Identity);
        }
        int at = parts.FindIndex(p => p.Prefab == piece && p.Chance != 0f);
        if (at < 0)
        {
            return null;
        }
        parts[at] = parts[at] with { Chance = 0f };
        return (parts[at].Position, parts[at].Rotation);
    }

    /// <summary>A snap point set at a place in the list as the mod sets one (AddSnapPoint): past the end, added at the end.</summary>
    private static void SetSnap(List<(NVector3, Quaternion)> snaps, (NVector3, Quaternion) snap, int index)
    {
        if (snaps.Count <= index)
        {
            snaps.Add(snap);
        }
        else
        {
            snaps[index] = snap;
        }
    }
}
