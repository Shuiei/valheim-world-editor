namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    /// <summary>The door types a joint of this type can take in this kind of dungeon.</summary>
    public static IEnumerable<(string Prefab, string Type, float Chance)> DoorsFor(Kind kind, string type) => kind.Doors.Where(d => d.Type == type);

    /// <summary>The game's roll for a door at a joint (FindDoorType, then its chance).</summary>
    /// <param name="kind">The dungeon kind, for its door types.</param>
    /// <param name="joint">Where the rooms meet.</param>
    /// <param name="random">Numbers from 0 to 1; called twice when a door can stand there.</param>
    /// <returns>The door's prefab, or null.</returns>
    public static string? RollDoor(Kind kind, Joint joint, Func<float> random)
    {
        var types = DoorsFor(kind, joint.Type).ToList();
        if (!joint.DoorAllowed || types.Count == 0)
        {
            return null;
        }
        var door = types[Math.Min(types.Count - 1, (int)(random() * types.Count))];
        float chance = door.Chance > 0 ? door.Chance : kind.DoorChance;
        return random() <= chance ? door.Prefab : null;
    }
}
