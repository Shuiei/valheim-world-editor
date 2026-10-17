using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>Rooms that can lead on to others.</summary>
    private static bool CanParent(DungeonPlan.Space s) => s.IsRoom && s.Room is not (Room.StairTop or Room.Boss or Room.Treasury or Room.Secret);

    /// <summary>Rooms that can lead on to a room of kind <paramref name="child"/>: the arena leads only to its treasury.</summary>
    private static bool CanParent(DungeonPlan.Space s, Room child) => CanParent(s) || s.Room == Room.Boss && child == Room.Treasury;

    /// <summary>A random space of the level that can lead on (on the main way if <paramref name="main"/>), else its first.</summary>
    private DungeonPlan.Space AnyParent(DungeonPlan.Level l, bool main) =>
        l.Spaces.Where(s => CanParent(s) && (!main || s.Main)).OrderBy(_ => _rnd.Next()).FirstOrDefault() ?? l.Spaces[0];

    /// <summary>The kinds of room that belong beside a kind: what a wing is made of.</summary>
    public static Room[] Kin(Room room) => room switch
    {
        Room.Kitchen => [Room.Feast, Room.Store],
        Room.Feast => [Room.Kitchen, Room.Store, Room.Quarters],
        Room.Store => [Room.Kitchen, Room.Forge, Room.Workshop, Room.Barracks, Room.Mine],
        Room.Barracks => [Room.Guard, Room.Armory, Room.Feast, Room.Store],
        Room.Guard => [Room.Cells, Room.Barracks, Room.Armory],
        Room.Armory => [Room.Barracks, Room.Guard, Room.Forge],
        Room.Cells => [Room.Torture, Room.Guard],
        Room.Torture => [Room.Cells, Room.Guard],
        Room.Library => [Room.Quarters, Room.Chapel, Room.Workshop],
        Room.Quarters => [Room.Library, Room.Kitchen, Room.Feast],
        Room.Chapel => [Room.Shrine, Room.Tomb, Room.Library],
        Room.Shrine => [Room.Chapel, Room.Tomb, Room.Ossuary],
        Room.Burial => [Room.Ossuary, Room.Tomb, Room.Shrine],
        Room.Ossuary => [Room.Burial, Room.Tomb],
        Room.Tomb => [Room.Burial, Room.Shrine, Room.Ossuary],
        Room.Forge => [Room.Workshop, Room.Store, Room.Mine, Room.Armory],
        Room.Workshop => [Room.Forge, Room.Store, Room.Library],
        Room.Mine => [Room.Workshop, Room.Store, Room.Forge],
        Room.Den => [Room.Totem, Room.Store, Room.Den],
        Room.Totem => [Room.Den, Room.Cells],
        _ => [],
    };

    /// <summary>A space to put a side room off, weighted towards crossings and long halls; null when none can lead on.</summary>
    private DungeonPlan.Space? SideParent(DungeonPlan.Level l)
    {
        var options = l.Spaces.Where(s => CanParent(s) && s.Room != Room.Antechamber)
            .Select(s => (s, s.Room switch { Room.Hub => 3f, Room.Hall or Room.Gallery or Room.Mine => 2.5f, Room.StairBottom => 0.5f, _ => s.Main ? 1.5f : 0.6f }))
            .ToList();
        return options.Count == 0 ? null : Pick(options);
    }
}
