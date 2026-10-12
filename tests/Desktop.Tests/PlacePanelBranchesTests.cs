using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.Terrain;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place panel's less common paths on the test world (zone 0, 0): Delete on a built-in preset, declined, or on one
/// gone from the memory; the note for kinds that need cultivated ground; the info line's end-to-end gap and the turned
/// grid; and a panel of its own with no one listening.
/// </summary>
[Collection("World files")]
public class PlacePanelBranchesTests
{
    /// <summary>
    /// A main window on its own copy of the test world (zone 0, 0) in the Place tool, with the panel's memory emptied;
    /// deleted when disposed.
    /// </summary>
    private sealed class Run : IDisposable
    {
        public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
        public string Dir { get; } = Fixtures.CopyFixture();
        public WorldScene Scene { get; }
        public PlacePanel P => W.PlacePanel;
        public PlaceTool T => W.PlaceTool;

        public Run()
        {
            Scene = WorldScene.Load(Dir, 0, 0, 1);
            W.Show();
            W.View.Show(Scene, null);
            W.Edit(Scene.Session!);
            W.Tools.ChooseMode(ToolMode.Place);
            P.Memory.Presets.Clear();
            P.Memory.Favourites.Clear();
            P.Memory.Recent.Clear();
        }

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
    }

    /// <summary>
    /// Clicks the button and runs the posted work it starts.
    /// </summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Chooses these kinds, as ticking them in the panel does.
    /// </summary>
    private static void Choose(Run r, params string[] names)
    {
        r.T.Chosen.Clear();
        r.T.Chosen.AddRange(names);
        typeof(PlacePanel).GetMethod("Chosen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(r.P, [true]);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void DeleteLeavesBuiltInDeclinedAndVanishedPresetsAlone()
    {
        using var r = new Run();
        int entries = r.P.PresetBox.ItemCount;
        // A built-in preset: not yours to delete.
        r.P.PresetBox.SelectedIndex = 1;
        Click(r.P.DeletePresetButton);
        Assert.Equal(entries, r.P.PresetBox.ItemCount);
        // One of yours, the question declined.
        Choose(r, "Beech1");
        r.P.AskName = () => Task.FromResult<string?>("Mine");
        Click(r.P.SavePresetButton);
        Assert.Single(r.P.Memory.Presets);
        r.P.Confirm = _ => Task.FromResult(false);
        Click(r.P.DeletePresetButton);
        Assert.Single(r.P.Memory.Presets);
        // Gone from the memory meanwhile: the entry points past the end, nothing is deleted.
        r.P.Memory.Presets.Clear();
        r.P.Confirm = _ => Task.FromResult(true);
        Click(r.P.DeletePresetButton);
        Assert.Equal(entries + 1, r.P.PresetBox.ItemCount);
    }

    [AvaloniaFact]
    public void TheNoteNamesEveryKindThatNeedsCultivatedGround()
    {
        using var r = new Run();
        Choose(r, "Beech1");
        Assert.Equal("", r.P.Note.Text);
        Assert.False(r.P.Note.IsVisible);
        Choose(r, "sapling_carrot", "sapling_turnip");
        Assert.Contains("sapling_carrot, sapling_turnip only grow on cultivated ground", r.P.Note.Text);
    }

    [AvaloniaFact]
    public void TheInfoTellsTheGapLeftEndToEndAndTheShapesTurn()
    {
        using var r = new Run();
        Click(r.P.ModeButtons[PlaceTool.Modes.Line]);
        Choose(r, "woodwall");
        Assert.True(r.T.EndToEnd);
        r.T.SnapTo = false;
        // 5 m: two 2 m walls, 1 m left.
        r.T.Points.Add(new Vector2(20, 32));
        r.T.Points.Add(new Vector2(25, 32));
        r.T.Notify();
        Assert.Contains("end to end (1.0 m of the line left at the end", r.P.Info.Text);
        Click(r.P.ModeButtons[PlaceTool.Modes.Grid]);
        r.T.GridA = new Vector2(10, 10);
        r.T.GridB = new Vector2(30, 30);
        Assert.True(r.T.TurnShape(90));
        Assert.Contains("turn the grid (now 90°)", r.P.Info.Text);
        Click(r.P.ModeButtons[PlaceTool.Modes.Zone]);
        Assert.Contains("turn the zone", r.P.Info.Text);
    }

    [AvaloniaFact]
    public void APanelOfItsOwnSaysNothingAndTellsNoOneOfOpenedGroups()
    {
        using var r = new Run();
        var input = new PlaceInput(r.W.View, new PlaceTool { Scene = () => r.Scene });
        var p = new PlacePanel(input, () => r.Scene, PrefabCatalog.NameOf, new PlaceMemory());
        Click(p.PickButton);
        Assert.NotNull(input.PickOnce);
        Click(p.KindsButton);
        var (kind, group) = p.Groups.First(g => !g.Value.IsExpanded);
        group.IsExpanded = true;
        Assert.Contains(kind, p.OpenKinds);
    }
}
