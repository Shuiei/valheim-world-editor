using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Dungeon panel's line about the open dungeon: its overlapping rooms, and the warning for the room chosen in the
/// list where it would go; the dungeon list switches the tool's dungeon, Inside cuts the roofs and leaving takes the cut
/// away.
/// </summary>
[Collection("World files")]
public class DungeonPanelInfoTests
{
    private static readonly int Cave = StableHash.Of("DG_Cave");
    private static readonly Vector3 At = new(10, 5100, 10);
    private static readonly Vector3 Other = new(-20, 5100, -20);

    /// <summary>
    /// A Frost Cave object at a place, with these rooms.
    /// </summary>
    private static (NewObject, bool) CaveAt(Vector3 at, params Dungeons.Placed[] rooms)
    {
        byte[] blank = ZdoBuilder.Blank(Cave, Terrain.PrefabCatalog.Get(Cave)!.Flags, at, Vector3.Zero, 0f);
        return (new NewObject(0, Cave, at, Vector3.Zero, 0, null, false, Dungeons.WithRooms(blank, rooms)), false);
    }

    [AvaloniaFact]
    public void TheInfoSaysWhatOverlapsAndThePanelsChoicesReachTheTool()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            w.View.Show(scene, null);
            w.Edit(scene.Session!);
            // The entrance, a copy of it half a metre up (the two overlap), and a corridor 1 m above where one
            // would join the entrance's first opening.
            var entrance = new Dungeons.Placed(StableHash.Of("cave_new_entrance02"), At, Quaternion.Identity);
            var end = Dungeons.Openings([entrance], freeOnly: true)[0];
            var corridor = Dungeons.RoomOf("cave_new_corridor03")!;
            var joined = Dungeons.Attach(corridor, Dungeons.Matching(corridor, end.Type).First(), end.Position, end.Rotation);
            scene.Session!.Commit("Dungeons", null, [],
            [
                CaveAt(At, entrance, joined with { Position = joined.Position + Vector3.UnitY }, entrance with { Position = At + new Vector3(0, 0.5f, 0) }),
                CaveAt(Other, entrance with { Position = Other }),
            ]);
            w.Tools.ChooseMode(ToolMode.Dungeon);
            Dispatcher.UIThread.RunJobs();
            var p = w.DungeonPanel;
            var t = w.View.Dungeon;
            var names = ((IEnumerable<string>)p.DungeonBox.ItemsSource!).ToList();
            Assert.Equal(2, names.Count);
            // The other dungeon chosen in the list, then the first again.
            p.DungeonBox.SelectedIndex = names.FindIndex(n => n.Contains("1 rooms", StringComparison.Ordinal));
            Assert.Single(t.Dungeon!.Rooms);
            Assert.DoesNotContain("overlapping", p.Info.Text, StringComparison.Ordinal);
            p.DungeonBox.SelectedIndex = names.FindIndex(n => n.Contains("3 rooms", StringComparison.Ordinal));
            Assert.Equal(3, t.Dungeon!.Rooms.Count);
            Assert.Contains("1 overlapping pair(s).", p.Info.Text, StringComparison.Ordinal);
            // A corridor chosen in the list, pointed (from below) at the entrance's first opening: it would
            // run into the raised one.
            p.Rooms.SelectedItem = p.Rooms.Items.Cast<ListBoxItem>().First(i => (string)i.Tag! == "cave_new_corridor03");
            Assert.Equal("cave_new_corridor03", t.Chosen);
            t.Hover(end.Position - new Vector3(0, 30, 0), Vector3.UnitY);
            Assert.Equal(0, t.HoverEnd);
            Assert.Contains("This one: Overlaps cave_new_corridor03.", p.Info.Text, StringComparison.Ordinal);
            // Inside: the roofs cut 4 m above the entrance's floor.
            p.GoInside.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(4, p.CutSlider.Value);
            float? cut = At.Y - 3 + 4;
            Assert.Equal(cut, w.View.CutY);
            // Another tool: the cut off.
            w.Tools.ChooseMode(ToolMode.Select);
            Assert.Null(w.View.CutY);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
