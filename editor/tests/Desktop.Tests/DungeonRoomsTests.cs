using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The dungeons of an area as the view draws them (read on a worker after an edit, what was read before shown
/// meanwhile), one the game has not laid out yet, and the names shown for the kinds.
/// </summary>
public class DungeonRoomsTests
{
    [AvaloniaFact]
    public async Task TheViewGetsWhatWasReadAndTheDungeonsAreReadAgainAfterAnEdit()
    {
        // A frost cave's object never laid out (no room data), and a tree.
        var cave = new WorldScene.Thing(-1, StableHash.Of("DG_Cave"), new Vector3(0, 5000, 0), Vector3.Zero, 1, false);
        var tree = new WorldScene.Thing(-2, StableHash.Of("Beech1"), new Vector3(5, 30, 5), Vector3.Zero, 1, false);
        var s = TestScenes.Flat(1, tree, cave);
        int ready = 0;
        // Nothing read yet: none for now, read on a worker.
        Assert.Empty(DungeonRooms.Shown(s.Scene, () => Interlocked.Increment(ref ready)));
        await TestWindows.Until(() => Volatile.Read(ref ready) == 1);
        var shown = Assert.Single(DungeonRooms.Shown(s.Scene, () => Interlocked.Increment(ref ready)));
        Assert.Equal(1, shown.Index);
        Assert.Equal("DG_Cave", shown.Kind.Name);
        Assert.Empty(shown.Rooms);
        Assert.Equal("The game has not laid this dungeon out yet (nobody has been near it).", shown.Problem?.English);
        // The same read is kept while nothing changes.
        Assert.Same(DungeonRooms.Of(s.Scene), DungeonRooms.Of(s.Scene));
        // An object more: read again; what was read before is shown meanwhile.
        lock (s.Scene.Things)
        {
            s.Scene.Things.Add(cave with { Id = -3, Position = new Vector3(0, 5000, 60) });
        }
        Assert.Single(DungeonRooms.Shown(s.Scene, () => Interlocked.Increment(ref ready)));
        await TestWindows.Until(() => Volatile.Read(ref ready) == 2);
        Assert.Equal(2, DungeonRooms.Shown(s.Scene, () => { }).Count);
    }

    [Fact]
    public void ADungeonGoneInTheLiveGameIsReadAgain()
    {
        // The game took the cave away: changed in place (no edit, no object more), the cache kept showing it.
        var cave = new WorldScene.Thing(-1, StableHash.Of("DG_Cave"), new Vector3(0, 5000, 0), Vector3.Zero, 1, false);
        var s = TestScenes.Flat(1, cave);
        Assert.Single(DungeonRooms.Of(s.Scene));
        Assert.True(s.TakeGameChanges(new WorldSession.Followed(new WorldSave.Merged([], [], [-1], []), [], [], [])));
        Assert.Empty(DungeonRooms.Of(s.Scene));
    }

    [Theory]
    [InlineData("DG_Cave", "Frost Cave")]
    [InlineData("DG_SunkenCrypt", "Sunken Crypt")]
    [InlineData("DG_ForestCrypt", "Burial Chambers")]
    [InlineData("DG_HalfBurried_ForestCrypt", "Burial Chambers (half buried)")]
    [InlineData("DG_DvergrTown", "Infested Mine")]
    [InlineData("DG_DvergrBoss", "Infested Citadel")]
    [InlineData("DG_Hildir_Cave", "Howling Cavern (Hildir)")]
    [InlineData("DG_Hildir_ForestCrypt", "Smouldering Tomb (Hildir)")]
    [InlineData("DG_Hildir_PlainsFortress", "Sealed Tower (Hildir)")]
    [InlineData("DG_GoblinCamp", "Fuling village")]
    [InlineData("DG_MeadowsVillage", "Meadows village")]
    [InlineData("DG_MeadowsFarm", "Meadows farm")]
    [InlineData("DG_AshlandsFortress_Big", "Ashlands Fortress Big")]
    [InlineData("DG_NewPlace", "New Place")]
    public void EachKindHasANameToShow(string prefab, string shown) =>
        Assert.Equal(shown, DungeonTool.KindName(new Dungeons.Kind(prefab, StableHash.Of(prefab), 0, true, new Vector3(64, 64, 64))).English);
}
