using System.Numerics;
using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area panel on a copy of the test world: paint brought back from a backup onto unpainted ground (fully inside,
/// part way at the soft edge), a restore when the selection was dropped, and the eyedropper's kind put in the With list.
/// </summary>
[Collection("World files")]
public class AreaPanelBackupTests
{
    /// <summary>
    /// A window on zone 0, 0 of a copy of the test world, in the Area tool, a box from grid (10, 10) to (54, 54) with a
    /// 10 m soft edge selected.
    /// </summary>
    private static (MainWindow W, EditSession S, string Dir) Open()
    {
        string dir = Fixtures.CopyFixture();
        var scene = WorldScene.Load(dir, 0, 0, 1);
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        w.View.Show(scene, null);
        w.Edit(scene.Session!);
        w.Tools.ChooseMode(ToolMode.Area);
        w.AreaPanel.SoftSlider.Value = 10;
        w.View.Area.Points.Add(new Vector2(10, 10));
        w.View.Area.Points.Add(new Vector2(54, 54));
        w.View.Area.Notify();
        return (w, scene.Session!, dir);
    }

    /// <summary>
    /// Sets or clears dirt paint over grid (5, 5) to (59, 59).
    /// </summary>
    private static void Paint(EditSession s, bool on) => s.EditGround("paint", g =>
    {
        var touched = new List<int>();
        for (int z = 5; z < 60; z++)
        {
            for (int x = 5; x < 60; x++)
            {
                int p = z * g.W + x;
                g.PMod[p] = (byte)(on ? 1 : 0);
                Array.Copy(on ? Brush.PaintOf(BrushTool.PaintDirt)! : new float[4], 0, g.Paint, p * 4, 4);
                touched.Add(p);
            }
        }
        return (touched, (4, 4, 61, 61));
    });

    [AvaloniaFact]
    public async Task PaintComesBackFromABackupFadingAtTheSoftEdge()
    {
        var (w, s, dir) = Open();
        string backup = dir.TrimEnd(Path.DirectorySeparatorChar) + "_painted";
        try
        {
            // Painted and saved: the backup; then the paint cleared (not saved).
            Paint(s, true);
            Assert.True(s.Save().Saved);
            TempWorld.CopyDir(dir, backup);
            Paint(s, false);
            var p = w.AreaPanel;
            foreach (var b in p.KindButtons.Values)
            {
                b.IsChecked = false;
            }
            var poly = w.View.Area.Polygon()!;
            await p.RestoreBackup(s, poly, backup);
            Assert.StartsWith("Restored from the backup: the ground, 0 object(s) brought back, 0 removed.", w.MessageText.Text);
            var g = s.Ground;
            int mid = 32 * g.W + 32, edge = 32 * g.W + 12;
            Assert.Equal(1, g.PMod[mid]);
            Assert.Equal(1, g.Paint[mid * 4], 3);
            Assert.Equal(0, g.Paint[mid * 4 + 1], 3);
            Assert.Equal(1, g.PMod[edge]);
            Assert.InRange(g.Paint[edge * 4], 0.01f, 0.99f);
            Assert.Equal(1, g.Paint[edge * 4 + 3], 3);
            // The selection dropped (the outline still given): no ground to restore, so nothing.
            w.View.Area.Clear();
            await p.RestoreBackup(s, poly, backup);
            Assert.Equal("Nothing to restore: the selection is as it was in the backup.", w.MessageText.Text);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public void TheEyedropperPutsAKindTheGameCanMakeInTheWithList()
    {
        var (w, s, dir) = Open();
        try
        {
            var p = w.AreaPanel;
            int kind = s.Scene.World.Creatable.First(c => Terrain.PrefabCatalog.DisplayName(c) != null);
            string? asked = null;
            p.PickKindAsked += (action, use) => { asked = action; use(kind); };
            p.ToPickButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal("Replace", asked);
            Assert.True(p.ToBox.SelectedIndex >= 0);
            Assert.StartsWith("Replace with ", w.MessageText.Text);
            // A kind the game cannot make.
            p.UsePicked(12345);
            Assert.Equal("12345 cannot be placed: the game has no such kind to copy.", w.MessageText.Text);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
