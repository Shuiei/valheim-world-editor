using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedButtons;
using static TerrainEditor.Desktop.Tests.TipChecks;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Every control explains itself when hovered, as the web editor's did (Tips): each button, switch, list, slider and
/// text field of the editor (with every tool, Area action and Place mode, the Mask, the right-hand panels), the map
/// page, the start page and Settings has a tip, shown or not yet (a hidden one shows in another state). The few without
/// one are listed with the reason in TipChecks.
/// </summary>
[Collection("World files")]
public class TipsTests
{
    [AvaloniaFact]
    public void EveryControlOfTheEditorHasATip()
    {
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
        var w = TestWindows.Editing(s);
        var missing = new HashSet<string>();
        Check(missing, w);
        foreach (var t in Enum.GetValues<BrushTool>())
        {
            w.Tools.Choose(t);
            Check(missing, w);
        }
        w.MaskPanel.OnBox.IsChecked = true;
        Check(missing, w);
        foreach (var m in new[] { ToolMode.Measure, ToolMode.Shape, ToolMode.Path, ToolMode.View })
        {
            w.Tools.ChooseMode(m);
            Check(missing, w);
        }
        w.Tools.ChooseMode(ToolMode.Area);
        for (int i = 0; i < w.AreaPanel.ActionBox.ItemCount; i++)
        {
            w.AreaPanel.ActionBox.SelectedIndex = i;
            Check(missing, w);
        }
        w.Tools.ChooseMode(ToolMode.Place);
        w.PlaceTool.Chosen.Clear();
        w.PlaceTool.Chosen.AddRange(["Beech1", "woodwall"]);
        // The window's Place panel took the kinds remembered by earlier tests (pieces from the Workshop turn
        // One at a time and End to end on): set again for the kinds chosen here, as choosing them in the panel does.
        w.PlaceTool.AutoSnap();
        foreach (var m in Enum.GetValues<PlaceTool.Modes>())
        {
            Click(w.PlacePanel.ModeButtons[m]);
            Check(missing, w);
        }
        Click(w.PlacePanel.KindsButton);
        // A starred kind: the favourites' chips.
        w.PlacePanel.Memory.ToggleFavourite("Beech1");
        Click(w.PlacePanel.KindsButton);
        Click(w.PlacePanel.KindsButton);
        Check(missing, w);
        w.Tools.ChooseSelect();
        w.View.Select([0]);
        Check(missing, w);
        w.Inspect();
        Check(missing, w);
        Click(w.AreaPanel.LibraryButton);
        Check(missing, w);
        foreach (var right in new Control?[] { w.History.Card, w.HelpCard, null })
        {
            w.ShowRight(right);
            Check(missing, w);
        }
        var box = new AreaTool { Soft = 0 };
        box.Points.Add(new Vector2(32, 32));
        box.Points.Add(new Vector2(48, 48));
        w.View.Paste.Clip = CopyData.FromArea(box, s.Ground, s.Scene, [], _ => null);
        w.StartPaste();
        Check(missing, w);
        NoneMissing(missing);
    }

    [AvaloniaFact]
    public async Task EveryControlOfTheMapHasATip()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var w = await TestWindows.OpenedWorld(dir);
            var missing = new HashSet<string>();
            Check(missing, w);
            w.MapPage!.Pick(0, 0);
            Check(missing, w);
            NoneMissing(missing);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, true);
        }
    }

    [AvaloniaFact]
    public void EveryControlOfTheStartPageHasATip()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        // A saved server: its row's fields and buttons.
        ServerConfig.Remember(new ServerConfig.Server { Name = "Tips", Host = "tips.example", User = "valheim" });
        w.ShowStart();
        var page = w.StartPage!;
        page.FillServers();
        var missing = new HashSet<string>();
        foreach (string? mode in new[] { "game", "server", "offline" })
        {
            page.SetMode(mode);
            Check(missing, w);
        }
        page.SetMode("server");
        page.ServerForm.IsExpanded = true;
        Check(missing, w);
        ServerConfig.Forget("valheim@tips.example:22");
        NoneMissing(missing);
    }

    [AvaloniaFact]
    public void EveryControlOfSettingsHasATip()
    {
        var owner = new Window();
        owner.Show();
        var settings = new AppSettings { BepInExFolders = { "/b" }, WorldFolders = { "/w" } };
        _ = SettingsDialog.Show(owner, settings);
        Dispatcher.UIThread.RunJobs();
        var dialog = Assert.Single(owner.OwnedWindows);
        var missing = new HashSet<string>();
        Check(missing, dialog);
        dialog.Close();
        NoneMissing(missing);
    }
}
