using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.ComboBoxChoices;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Dungeon panel with two dungeons in the area: choosing the one the game has not laid out yet says so, a room
/// chosen in the list is the tool's, leaving takes the cut away, and with no dungeon open Inside does nothing.
/// </summary>
[Collection("World files")]
public class DungeonPanelChoiceTests
{
    private static readonly int Cave = StableHash.Of("DG_Cave");

    [AvaloniaFact]
    public void ChoosingADungeonNotLaidOutYetSaysSo()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var w = TestWindows.Editing(scene.Session!);
            // Nothing open yet: Inside does nothing.
            w.DungeonPanel.Inside();
            Assert.Null(w.View.CutY);
            // A Frost Cave of one room, and one the game has not laid out (no rooms).
            var at = new Vector3(10, 5100, 10);
            byte[] blank = ZdoBuilder.Blank(Cave, Terrain.PrefabCatalog.Get(Cave)!.Flags, at, Vector3.Zero, 0f);
            var entrance = new Dungeons.Placed(StableHash.Of("cave_new_entrance02"), at, Quaternion.Identity);
            var other = new Vector3(-20, 5100, -20);
            scene.Session!.Commit("Dungeons", null, [],
            [
                (new NewObject(0, Cave, at, Vector3.Zero, 0, null, false, Dungeons.WithRooms(blank, [entrance])), false),
                (new NewObject(0, Cave, other, Vector3.Zero, 0, null, false, ZdoBuilder.Blank(Cave, Terrain.PrefabCatalog.Get(Cave)!.Flags, other, Vector3.Zero, 0f)), false),
            ]);
            w.Tools.ChooseMode(ToolMode.Dungeon);
            Dispatcher.UIThread.RunJobs();
            var p = w.DungeonPanel;
            Assert.Equal(2, Lines(p.DungeonBox).Count);
            // The one with no rooms.
            Choose(p.DungeonBox, "0 room(s)");
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(w.View.Dungeon.Dungeon!.Rooms);
            Assert.Contains("The game has not laid this dungeon out yet", p.Info.Text);
            // The laid out one, and a room chosen in its list.
            Choose(p.DungeonBox, "1 room(s)");
            Dispatcher.UIThread.RunJobs();
            Assert.Single(w.View.Dungeon.Dungeon!.Rooms);
            var room = p.Rooms.Items.Cast<ListBoxItem>().First(i => (string)i.Tag! == "cave_new_corridor03");
            p.Rooms.SelectedItem = room;
            Assert.Equal("cave_new_corridor03", w.View.Dungeon.Chosen);
            // Inside: the cut on; leaving: off.
            p.Inside();
            Assert.NotNull(w.View.CutY);
            p.Leave();
            Assert.Null(w.View.CutY);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
