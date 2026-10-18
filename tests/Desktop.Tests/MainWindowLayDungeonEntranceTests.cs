using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A dungeon of the game's rooms whose kind has no entrance (laid from its own origin) that cannot be laid: one without
/// rooms, and one whose dungeon object this world cannot make; each said, nothing added.
/// </summary>
public sealed class MainWindowLayDungeonEntranceTests
{
    /// <summary>A kind of dungeon no game file knows: no entrance, no object to copy.</summary>
    private static readonly Dungeons.Kind Unknown = new("DG_NoSuchDungeon", StableHash.Of("DG_NoSuchDungeon"), 0, true, new Vector3(64, 64, 64));

    /// <summary>
    /// Places a made-up result of these rooms of the Unknown kind in a window editing a flat area.
    /// </summary>
    /// <returns>The status bar's message, and whether the area's history took a step.</returns>
    private static (string Said, bool Changed) Place(List<Dungeons.Placed> rooms)
    {
        var (w, s) = TestWindows.EditingFlat();
        int steps = s.UndoList.Count;
        w.PlaceGenerated(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms), new DungeonGen.Result([], Vector3.Zero, Unknown, rooms, [], "Nowhere"));
        return (w.MessageText.Text ?? "", s.UndoList.Count != steps);
    }

    [AvaloniaFact]
    public void ADungeonWithoutRoomsIsNotPlaced()
    {
        Assert.Equal(("Nowhere has no rooms: nothing placed.", false), Place([]));
    }

    [AvaloniaFact]
    public void ADungeonWhoseObjectCannotBeMadeIsNotPlaced()
    {
        Assert.Equal(("DG_NoSuchDungeon cannot be made in this world.", false), Place([new(StableHash.Of("cave_new_entrance02"), Vector3.Zero, Quaternion.Identity)]));
    }
}
