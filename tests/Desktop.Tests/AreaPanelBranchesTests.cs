using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area panel's less travelled paths on a flat area: a panel nobody listens to, the Mask limiting the object
/// actions, Reset zones off the ground, the backups of a world without a folder, and a picture not made by the editor
/// imported where the Mask lets nothing through.
/// </summary>
public class AreaPanelBranchesTests
{
    private static readonly int Beech = StableHash.Of("Beech1");

    /// <summary>
    /// A window on a flat 2 × 2 zone area (Meadows west of x = 64, Black Forest east) with a beech at grid (60, 60)
    /// and another at grid (68, 60), the Area tool on and a box from grid (50, 50) to (80, 80) around both.
    /// </summary>
    private static (MainWindow W, EditSession S) Open()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        // World x, z = grid + (-32).
        var s = TestScenes.Flat(2,
            new WorldScene.Thing(1, Beech, new Vector3(28, 30, 28), Vector3.Zero, 1, false),
            new WorldScene.Thing(2, Beech, new Vector3(36, 30, 28), Vector3.Zero, 1, false));
        for (int i = 0; i < s.Scene.Biomes.Length; i++)
        {
            s.Scene.Biomes[i] = i % s.Scene.W >= 64 ? 8 : 1;
        }
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseMode(ToolMode.Area);
        Select(w, 50, 50, 80, 80);
        return (w, s);
    }

    /// <summary>
    /// Selects the box between two grid points.
    /// </summary>
    private static void Select(MainWindow w, float x0, float z0, float x1, float z1)
    {
        w.View.Area.Clear();
        w.View.Area.Points.Add(new Vector2(x0, z0));
        w.View.Area.Points.Add(new Vector2(x1, z1));
        w.View.Area.Notify();
    }

    /// <summary>
    /// Clicks the button and runs the posted work it starts.
    /// </summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task APanelNobodyListensToStillWorks()
    {
        var (w, s) = Open();
        var p = new AreaPanel(w.View, () => s, Terrain.PrefabCatalog.DisplayName);
        foreach (var b in new[] { p.CopyButton, p.PasteButton, p.SaveBlueprintButton, p.LibraryButton, p.ToPickButton, p.UnresetButton })
        {
            Click(b);
        }
        p.UsePicked(12345);
        p.Replace(s, [], Beech);
        p.Choose(AreaPanel.Act.Remove);
        await p.Apply();
        Assert.Equal(2, s.Scene.Things.Count(t => t.Gone));
        p.Choose(AreaPanel.Act.Select);
        await p.Apply();
        Click(p.ClearButton);
        Assert.Empty(w.View.Area.Points);
        await p.Apply();
        Assert.Equal("Area: removed 2 object(s)", s.UndoLabel);
    }

    [AvaloniaFact]
    public async Task TheMaskLimitsTheObjectActions()
    {
        var (w, s) = Open();
        s.Mask.On = true;
        s.Mask.Biomes.Add(8);
        var p = w.AreaPanel;
        p.Choose(AreaPanel.Act.Remove);
        await p.Apply();
        // Only the beech in the Black Forest.
        Assert.False(s.Scene.Things[0].Gone);
        Assert.True(s.Scene.Things[1].Gone);
        Assert.StartsWith("Removed 1 object(s).", w.MessageText.Text);
    }

    [AvaloniaFact]
    public async Task ResetZonesOffTheGroundAsksForAnArea()
    {
        var (w, s) = Open();
        bool asked = false;
        w.AreaPanel.Confirm = _ => { asked = true; return Task.FromResult(true); };
        Select(w, -50, -50, -40, -40);
        w.AreaPanel.Choose(AreaPanel.Act.Reset);
        await w.AreaPanel.Apply();
        Assert.Equal("Select an area first.", w.MessageText.Text);
        Assert.False(asked);
        Assert.Empty(s.Resets);
    }

    [AvaloniaFact]
    public async Task AWorldWithoutAFolderListsOnlyTheFoldersChosen()
    {
        var (w, _) = Open();
        var p = w.AreaPanel;
        p.Choose(AreaPanel.Act.Backup);
        Assert.Equal("Another folder…", Assert.Single(((IEnumerable<object>)p.BackupBox.ItemsSource!).Cast<string>()));
        await p.Apply();
        Assert.Equal("Choose a backup first.", w.MessageText.Text);
        string folder = Path.Combine(Path.GetTempPath(), "vwe-no-backup-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        try
        {
            p.PickFolder = () => Task.FromResult<string?>(folder);
            p.BackupBox.SelectedIndex = 0;
            await TestWindows.Until(() => p.BackupBox.SelectedIndex == 0 && p.BackupBox.ItemCount == 2);
            // Another action and back: the folder chosen stays chosen.
            p.Choose(AreaPanel.Act.Flatten);
            p.Choose(AreaPanel.Act.Backup);
            Assert.Equal(0, p.BackupBox.SelectedIndex);
            Assert.Equal(folder, ((IEnumerable<object>)p.BackupBox.ItemsSource!).Cast<string>().First());
            // An empty folder: said so.
            await p.Apply();
            Assert.StartsWith("That folder is not a world save that can be read", w.MessageText.Text);
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    [AvaloniaFact]
    public void APictureWithoutHeightsWhereTheMaskLetsNothingThroughChangesNothing()
    {
        var (w, s) = Open();
        string file = Path.Combine(Path.GetTempPath(), "vwe-pic-" + Guid.NewGuid().ToString("N")[..8] + ".png");
        try
        {
            File.WriteAllBytes(file, Png.WriteGray16(4, 4, [.. Enumerable.Range(0, 16).Select(i => (ushort)(i * 4000))]));
            w.View.Area.Clear();
            s.Mask.On = true;
            s.Mask.HeightMin = 100;
            var p = w.AreaPanel;
            p.LoadPicture(file);
            Assert.Equal(0m, p.LowestBox.Value);
            Assert.Equal(0m, p.HighestBox.Value);
            Assert.Equal("Picture 4 × 4 fitted to 129 × 129 m (the whole area).", p.PictureInfo.Text);
            p.PutPicture();
            Assert.Equal("Imported the heightmap into 0 point(s). Ctrl+Z undoes it.", w.MessageText.Text);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
