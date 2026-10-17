using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Dungeon tool on the test world (zone 0, 0), with a Frost Cave of one room made there (the test world has no
/// dungeon): the panel lists it, a room picked and clicked onto an open end joins there, the open ends close with end
/// caps, a selected room is deleted, and each change is one step of the history that rewrites the dungeon object's room
/// list; a room placed at an open end without the pointer (PlaceAt), and changes named as asked.
/// </summary>
[Collection("World files")]
public class DungeonToolTests
{
    private static readonly int Cave = StableHash.Of("DG_Cave");
    private static readonly Vector3 At = new(10, 5100, 10);

    /// <summary>
    /// A window on a copy of the test world with a one-room Frost Cave committed at At and the Dungeon tool on; Said
    /// keeps the tool's last message.
    /// </summary>
    private sealed class Run : IDisposable
    {
        public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
        public string Dir { get; } = Fixtures.CopyFixture();
        public WorldScene Scene { get; }
        public DungeonTool T => W.View.Dungeon;
        public DungeonPanel P => W.DungeonPanel;
        public string? Said;

        public Run()
        {
            Scene = WorldScene.Load(Dir, 0, 0, 1);
            W.Show();
            W.View.Show(Scene, null);
            W.Edit(Scene.Session!);
            // A Frost Cave of one room: its entrance, three open ends.
            var entrance = new Dungeons.Placed(StableHash.Of("cave_new_entrance02"), At, Quaternion.Identity);
            byte[] blank = ZdoBuilder.Blank(Cave, Terrain.PrefabCatalog.Get(Cave)!.Flags, At, Vector3.Zero, 0f);
            Scene.Session!.Commit("A dungeon", null, [], [(new NewObject(0, Cave, At, Vector3.Zero, 0, null, false, Dungeons.WithRooms(blank, [entrance])), false)]);
            T.Message += t => Said = t.Shown;
            W.Tools.ChooseMode(ToolMode.Dungeon);
        }

        public List<Dungeons.Placed> Rooms => T.Dungeon!.Rooms;

        // Points the tool at a free opening from straight above it.
        public void PointAt(Dungeons.OpenEnd end) => T.Hover(end.Position + new Vector3(0, 30, 0), -Vector3.UnitY);

        public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
    }

    /// <summary>
    /// Clicks a button.
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// The room list the dungeon object holds now (what Save to world writes).
    /// </summary>
    private static List<Dungeons.Placed> Saved(Run r)
    {
        var d = r.T.Dungeon!;
        byte[] bytes = ObjectData.Bytes(r.Scene.World, r.Scene.Session!.Edits, d.Thing.Id)!;
        return Dungeons.Read(ZdoData.Parse(bytes).GetBytes(Dungeons.RoomDataKey)!);
    }

    [AvaloniaFact]
    public void ThePanelListsTheAreasDungeonAndItsRooms()
    {
        using var r = new Run();
        Assert.True(r.P.Card.IsVisible);
        Assert.Equal("Frost Cave, 1 rooms, at 10, 10", Assert.Single((IEnumerable<string>)r.P.DungeonBox.ItemsSource!));
        Assert.NotNull(r.T.Dungeon);
        Assert.Equal(3, r.T.FreeEnds.Count);
        var names = r.P.Rooms.Items.Cast<ListBoxItem>().Select(i => (string)i.Tag!).ToList();
        Assert.Contains("cave_new_corridor03", names);
        Assert.DoesNotContain(names, n => n.StartsWith("sunkencrypt", StringComparison.Ordinal));
        Assert.Contains("3 open end(s)", r.P.Info.Text);
        // The finder narrows the list.
        r.P.Filter.Text = "endcap_painting";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotEmpty(r.P.Rooms.Items);
        Assert.All(r.P.Rooms.Items.Cast<ListBoxItem>(), i => Assert.Contains("endcap_painting", (string)i.Tag!));
    }

    [AvaloniaFact]
    public void ARoomClickedOntoAnOpenEndJoinsThereAndUndoTakesItAway()
    {
        using var r = new Run();
        r.T.Choose("cave_new_corridor03");
        var end = r.T.FreeEnds[0];
        r.PointAt(end);
        Assert.Equal(0, r.T.HoverEnd);
        var preview = r.T.Preview!;
        Assert.Null(r.T.Problem(preview));
        Assert.True(r.T.Click());
        Assert.Equal(2, r.Rooms.Count);
        Assert.Equal(2, Saved(r).Count);
        // The corridor's opening meets the entrance's: that one is not open any more, the corridor's
        // far end is.
        Assert.DoesNotContain(r.T.FreeEnds, e => Vector3.Distance(e.Position, end.Position) < 0.01f);
        Assert.Equal(3, r.T.FreeEnds.Count);
        Assert.Contains("Added cave_new_corridor03", r.Said);
        // One step back: the dungeon as it was.
        Assert.True(r.Scene.Session!.Undo());
        Assert.Single(r.Rooms);
        Assert.Single(Saved(r));
    }

    [AvaloniaFact]
    public void ASelectedRoomTakenAwayByUndoIsNotSelectedAnyMore()
    {
        using var r = new Run();
        r.T.Choose("cave_new_corridor03");
        r.PointAt(r.T.FreeEnds[0]);
        Assert.True(r.T.Click());
        Assert.Equal(1, r.T.Selected);
        // Undo takes the corridor away: index 1 names no room, and Delete removes nothing.
        Assert.True(r.Scene.Session!.Undo());
        Assert.Null(r.T.Selected);
        Assert.False(r.T.Delete());
        Assert.Single(r.Rooms);
        // Redo brings it back where it was: selected again.
        Assert.True(r.Scene.Session.Redo());
        Assert.Equal(1, r.T.Selected);
    }

    [AvaloniaFact]
    public void TurnJoinsByAnotherOpeningAndAnOverlapIsSaid()
    {
        using var r = new Run();
        // A crossroads has four openings: Turn tries the next one at the same open end.
        r.T.Choose("cave_new_crossroads01");
        r.PointAt(r.T.FreeEnds[0]);
        var first = r.T.Preview!;
        r.T.Turn();
        var second = r.T.Preview!;
        Assert.True(Vector3.Distance(first.Position, second.Position) > 1 || Quaternion.Dot(first.Rotation, second.Rotation) < 0.99f);
        // A big room at an end that faces the entrance's other side runs into it.
        r.T.Choose("cave_new_sloperoom05");
        string? problem = null;
        foreach (int e in Enumerable.Range(0, r.T.FreeEnds.Count))
        {
            r.PointAt(r.T.FreeEnds[e]);
            for (int k = 0; k < 8 && problem == null; k++, r.T.Turn())
            {
                problem = r.T.Preview is { } p ? r.T.Problem(p)?.Shown : null;
            }
        }
        Assert.NotNull(problem);
    }

    [AvaloniaFact]
    public void CloseOpenEndsCapsThemAllInOneStep()
    {
        using var r = new Run();
        int steps = r.Scene.Session!.UndoList.Count;
        Click(r.P.CloseEnds);
        Assert.Equal(4, r.Rooms.Count);
        Assert.Empty(r.T.FreeEnds);
        Assert.All(r.Rooms.Skip(1), p => Assert.True(p.Room!.EndCap));
        Assert.Equal(steps + 1, r.Scene.Session.UndoList.Count);
        Assert.False(r.P.CloseEnds.IsEnabled);
        Assert.Contains("Closed 3 open end(s)", r.Said);
    }

    [AvaloniaFact]
    public void AClickedRoomIsSelectedAndDeleted()
    {
        using var r = new Run();
        Click(r.P.CloseEnds);
        // From above the middle of the entrance room: its box.
        r.T.Hover(At + new Vector3(0, 40, 0), -Vector3.UnitY);
        Assert.Equal(0, r.T.HoverRoom);
        Assert.True(r.T.Click());
        Assert.Equal(0, r.T.Selected);
        // A cap, from inside it (the camera in a room): the smallest box around the eye wins.
        var cap = r.Rooms[1];
        r.T.Hover(cap.Position + new Vector3(0, 1, 0), -Vector3.UnitY);
        Assert.Equal(1, r.T.HoverRoom);
        r.T.Click();
        Assert.Equal(1, r.T.Selected);
        Assert.True(r.P.DeleteButton.IsEnabled);
        Click(r.P.DeleteButton);
        Assert.Equal(3, r.Rooms.Count);
        Assert.Single(r.T.FreeEnds);
        Assert.Null(r.T.Selected);
    }

    [AvaloniaFact]
    public void BuildHereSetsThePlaceToolForBuildingAndLeavingGivesItsSettingsBack()
    {
        using var r = new Run();
        var t = r.W.PlaceTool;
        var before = (t.Mode, t.Building, t.OneAtATime);
        Click(r.P.BuildHere);
        Assert.Equal(ToolMode.Place, r.W.Tools.Mode);
        Assert.True(r.W.BuildingInDungeon);
        Assert.True(t.Building);
        Assert.True(r.W.BuildPanel.Card.IsVisible);
        Assert.False(r.W.PlacePanel.Card.IsVisible);
        // Back to the Dungeon tool: the Place tool as it was.
        r.W.Tools.ChooseMode(ToolMode.Dungeon);
        Assert.False(r.W.BuildingInDungeon);
        Assert.Equal(before, (t.Mode, t.Building, t.OneAtATime));
        Assert.False(r.W.BuildPanel.Card.IsVisible);
    }

    [AvaloniaFact]
    public void BuildHereKeepsTheCutAndAnotherAreaEndsIt()
    {
        using var r = new Run();
        Click(r.P.GoInside);
        float? cut = r.W.View.CutY;
        Assert.NotNull(cut);
        Click(r.P.BuildHere);
        // Building under the dungeon's cut: the roofs stay away, the pieces go on the floors.
        Assert.Equal(cut, r.W.View.CutY);
        Assert.Equal(cut, r.W.PlaceTool.CutY);
        // Another area opened: building in this dungeon ends, its cut with it.
        r.W.Edit(r.Scene.Session!);
        Assert.False(r.W.BuildingInDungeon);
        Assert.Null(r.W.PlaceTool.CutY);
    }

    [AvaloniaFact]
    public void DeletingARoomTakesTheObjectsInItWithIt()
    {
        using var r = new Run();
        Click(r.P.CloseEnds);
        // A chest in a cap, and one in the entrance room.
        var cap = r.Rooms[1];
        int chest = StableHash.Of("piece_chest_wood");
        r.Scene.Session!.Commit("Chests", null, [],
        [
            (new NewObject(0, chest, cap.Position + new Vector3(0, -2, 0), Vector3.Zero, 0), true),
            (new NewObject(0, chest, At + new Vector3(4, -2, 0), Vector3.Zero, 0), true),
        ]);
        int before = r.Scene.Things.Count(t => !t.Gone && t.Prefab == chest);
        r.T.Hover(cap.Position + new Vector3(0, 1, 0), -Vector3.UnitY);
        r.T.Click();
        Assert.True(r.T.Delete());
        Assert.Equal(before - 1, r.Scene.Things.Count(t => !t.Gone && t.Prefab == chest));
        Assert.Contains("and the 1 object(s) in it", r.Said);
        // One step back: the room and its chest.
        r.Scene.Session.Undo();
        Assert.Equal(before, r.Scene.Things.Count(t => !t.Gone && t.Prefab == chest));
        Assert.Equal(4, r.Rooms.Count);
    }

    [AvaloniaFact]
    public void AnAddedRoomBringsWhatTheGamePutsInItUnlessSwitchedOff()
    {
        using var r = new Run();
        // Never a door (doors come by chance): only the room and its contents are counted.
        r.T.Random = () => 1f;
        int things = r.Scene.Things.Count;
        r.T.Choose("cave_new_crossroads01_ice");
        r.PointAt(r.T.FreeEnds[0]);
        var p = r.T.Preview!;
        var expected = Dungeons.Contents(p, r.T.Dungeon!.Kind, At, r.Scene.World.Seed);
        Assert.NotEmpty(expected);
        int steps = r.Scene.Session!.UndoList.Count;
        r.T.Click();
        // The dungeon object again, and the room's objects, in one step.
        Assert.Equal(things + 1 + expected.Count, r.Scene.Things.Count);
        Assert.Equal(steps + 1, r.Scene.Session.UndoList.Count);
        // The new room is selected, and the panel lets it be deleted at once.
        Assert.True(r.P.DeleteButton.IsEnabled);
        Assert.Contains($"with {expected.Count} object(s)", r.Said);
        Assert.All(expected, m => Assert.Contains(r.Scene.Things, t => !t.Gone && t.Prefab == StableHash.Of(m.Prefab) && Vector3.Distance(t.Position, m.Position) < 1e-3f));
        // Switched off: the room alone.
        r.P.Contents.IsChecked = false;
        r.T.Choose("cave_new_endcap02");
        r.PointAt(r.T.FreeEnds[0]);
        things = r.Scene.Things.Count;
        r.T.Click();
        Assert.Equal(things + 1, r.Scene.Things.Count);
    }

    [AvaloniaFact]
    public void DoorsComeWithAddedRoomsByChanceAndAClickOnAJointTogglesOne()
    {
        using var r = new Run();
        int ice = StableHash.Of("caverock_ice_pillar_wall");
        int Doors() => r.Scene.Things.Count(t => !t.Gone && t.Prefab == ice);
        r.P.Contents.IsChecked = false;
        // A roll that always puts the door: the new room's joint gets the cave's door for plain openings.
        r.T.Random = () => 0f;
        r.T.Choose("cave_new_corridor03");
        r.PointAt(r.T.FreeEnds[0]);
        r.T.Click();
        Assert.Equal(1, Doors());
        Assert.Contains("with a door", r.Said);
        var joint = Assert.Single(r.T.DoorJoints);
        Assert.Single(r.T.DoorsAt(joint));
        // A roll that never does: the next room comes alone.
        r.T.Random = () => 0.99f;
        r.PointAt(r.T.FreeEnds.First(e => e.Room == 1));
        r.T.Click();
        Assert.Equal(1, Doors());
        Assert.Equal(2, r.T.DoorJoints.Count);
        // A click on the empty joint puts a door; again takes it away.
        r.T.Choose(null);
        var empty = r.T.DoorJoints.First(j => r.T.DoorsAt(j).Count == 0);
        r.T.Hover(empty.Position + new Vector3(0, 1.5f, 0) + new Vector3(0, 20, 0), -Vector3.UnitY);
        Assert.NotNull(r.T.HoverJoint);
        r.T.Click();
        Assert.Equal(2, Doors());
        r.T.Click();
        Assert.Equal(1, Doors());
        // Deleting the corridor takes its doors with it.
        r.T.Hover(r.Rooms[1].Position + new Vector3(0, 0.5f, 0), -Vector3.UnitY);
        r.T.Click();
        Assert.Equal(1, r.T.Selected);
        r.T.Delete();
        Assert.Equal(0, Doors());
    }

    // PlaceAt: the room joined at an open end by the join-th of its fitting openings, counting round either way, as
    // Preview places it; no such end, no such room, or no fitting opening: nothing.
    [AvaloniaFact]
    public void ARoomIsPlacedAtAnOpenEndWithoutThePointer()
    {
        using var r = new Run();
        r.T.Choose("cave_new_crossroads01");
        r.PointAt(r.T.FreeEnds[0]);
        r.T.Turn();
        Assert.Equal(r.T.Preview, r.T.PlaceAt(0, "cave_new_crossroads01", 1));
        int fitting = Dungeons.Matching(Dungeons.RoomOf("cave_new_crossroads01")!, r.T.FreeEnds[0].Type).Count();
        Assert.Equal(r.T.PlaceAt(0, "cave_new_crossroads01", 1), r.T.PlaceAt(0, "cave_new_crossroads01", 1 + fitting));
        Assert.Equal(r.T.PlaceAt(0, "cave_new_crossroads01", fitting - 1), r.T.PlaceAt(0, "cave_new_crossroads01", -1));
        Assert.Null(r.T.PlaceAt(3, "cave_new_crossroads01", 0));
        Assert.Null(r.T.PlaceAt(0, "no_such_room", 0));
    }

    // Add, Remove, CloseOpenEnds and ToggleDoor name their step as asked.
    [AvaloniaFact]
    public void ChangesAreNamedAsAsked()
    {
        using var r = new Run();
        var s = r.Scene.Session!;
        Assert.True(r.T.Add(r.T.PlaceAt(0, "cave_new_corridor03", 0)!, "Mine: a corridor"));
        Assert.Equal("Mine: a corridor", s.UndoLabel);
        var joint = r.T.DoorJoints[0];
        if (r.T.DoorsAt(joint).Count > 0)
        {
            r.T.ToggleDoor(r.T.Dungeon!, joint);
        }
        r.T.ToggleDoor(r.T.Dungeon!, joint, "Mine: a door");
        Assert.Equal("Mine: a door", s.UndoLabel);
        Assert.True(r.T.Remove(1, "Mine: no corridor"));
        Assert.Equal("Mine: no corridor", s.UndoLabel);
        Assert.False(r.T.Remove(9));
        r.T.CloseOpenEnds("Mine: capped");
        Assert.Equal("Mine: capped", s.UndoLabel);
    }
}
