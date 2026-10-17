using TerrainEditor.App;

namespace TerrainEditor.Editing;

public static partial class DungeonGen
{
    /// <summary>
    /// The kind of dungeon to make: the settings' kind (or the biome's), or else a cave, when that kind has no rooms that
    /// join, since camps and villages are laid out on the ground rather than room to room.
    /// </summary>
    /// <param name="s">The settings.</param>
    /// <param name="notes">Where a note of the cave instead is added.</param>
    /// <returns>The kind to make.</returns>
    /// <exception cref="InvalidDataException">The game's data has neither rooms to join for the kind nor for a cave.</exception>
    private static Dungeons.Kind ChooseRoomKind(Settings s, List<Said> notes)
    {
        static bool Joinable(Dungeons.Kind k) => Dungeons.RoomsFor(k).Any(r => !r.Entrance && !r.EndCap && !r.Divider && r.Openings.Length > 0);
        var cave = Dungeons.KindOf(Save.StableHash.Of("DG_Cave"));
        string wanted = s.Kind ?? KindFor(s.Biome);
        var kind = Dungeons.Kinds.FirstOrDefault(k => k.Name == wanted) ?? cave;
        if (kind == null || !Joinable(kind))
        {
            if (cave == null || !Joinable(cave))
            {
                throw new InvalidDataException($"The game's data has no rooms to join for {wanted.Replace("DG_", "", StringComparison.Ordinal)}, and no cave to make instead "
                    + "(a game update may have changed its dungeons).");
            }
            // (No kind at all only when there is no cave either.)
            notes.Add(Said.Of("dungeon.msg.caveInstead", Describe(kind!)));
            kind = cave;
        }
        return kind;
    }
}
