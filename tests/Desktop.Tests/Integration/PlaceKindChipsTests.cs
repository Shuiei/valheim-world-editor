using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

using static TerrainEditor.Desktop.Tests.SharedKindChooser;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place panel's kind chips and categories: picking kinds from the world, ticking and unticking them, folding
/// the categories, and remembering which are open.
/// </summary>
[Collection("World files")]
public class PlaceKindChipsTests
{
    /// <summary>
    /// Clicks the button and runs the posted work it starts.
    /// </summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void PickFromWorldPlacesOnlyThatKindOrWithShiftAddsIt()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        var kinds = r.P.Creatable().Select(c => c.Name).Take(3).ToList();
        Choose(r, kinds[0]);
        Click(r.P.PickButton);
        Assert.Equal("Click an object to pick its kind for placing. Esc cancels.", r.Said);
        r.W.PlaceInput.PickOnce!(kinds[1], true);
        Assert.Equal(new[] { kinds[0], kinds[1] }, r.T.Chosen);
        Assert.Equal($"Placing {kinds[0]}, {kinds[1]}.", r.Said);
        // Shift + click on a ticked kind takes it away.
        Click(r.P.PickButton);
        r.W.PlaceInput.PickOnce!(kinds[1], true);
        Assert.Equal(new[] { kinds[0] }, r.T.Chosen);
        // A plain click: only that kind.
        Click(r.P.PickButton);
        r.W.PlaceInput.PickOnce!(kinds[2], false);
        Assert.Equal(new[] { kinds[2] }, r.T.Chosen);
        Assert.Equal($"Placing {kinds[2]}.", r.Said);
    }

    [AvaloniaFact]
    public void EscCancelsPickingFromTheWorld()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        string? said = null;
        r.W.PlaceInput.Message += t => said = t.Shown;
        Click(r.P.PickButton);
        Assert.True(r.W.PlaceInput.Key(Key.Escape, false, false));
        Assert.Null(r.W.PlaceInput.PickOnce);
        Assert.Equal("Picking cancelled.", said);
        // Nothing to cancel: Esc is not taken.
        Assert.False(r.W.PlaceInput.Key(Key.Escape, false, false));
    }

    [AvaloniaFact]
    public void AChipTicksItsKindOrWithShiftPlacesOnlyIt()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        var kinds = r.P.Creatable().Select(c => c.Name).Take(3).ToList();
        r.P.Memory.Favourites.AddRange(kinds);
        Choose(r, kinds[0]);
        r.P.ChipClicked(kinds[1], shift: false);
        Assert.Equal(new[] { kinds[0], kinds[1] }, r.T.Chosen);
        r.P.ChipClicked(kinds[1], shift: false);
        Assert.Equal(new[] { kinds[0] }, r.T.Chosen);
        r.P.ChipClicked(kinds[2], shift: true);
        Assert.Equal(new[] { kinds[2] }, r.T.Chosen);
        // The chips show what is ticked.
        var chips = r.P.Favourites.Children.OfType<ToggleButton>().ToList();
        Assert.Equal(kinds, chips.Select(c => (string)c.Tag!));
        Assert.Equal([false, false, true], chips.Select(c => c.IsChecked == true));
    }

    [AvaloniaFact]
    public void ShiftClickingAChipWithTheMousePlacesOnlyIt()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        var kinds = r.P.Creatable().Select(c => c.Name).Take(2).ToList();
        r.P.Memory.Favourites.AddRange(kinds);
        Choose(r, kinds[0]);
        Click(r.P.KindsButton);
        r.W.UpdateLayout();
        Point At(ToggleButton chip) => chip.TranslatePoint(new Point(chip.Bounds.Width / 2, chip.Bounds.Height / 2), r.W)!.Value;
        var chip = r.P.Favourites.Children.OfType<ToggleButton>().Single(c => (string)c.Tag! == kinds[1]);
        r.W.MouseDown(At(chip), MouseButton.Left, RawInputModifiers.Shift);
        r.W.MouseUp(At(chip), MouseButton.Left, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new[] { kinds[1] }, r.T.Chosen);
        // Without Shift: added.
        r.W.UpdateLayout();
        chip = r.P.Favourites.Children.OfType<ToggleButton>().Single(c => (string)c.Tag! == kinds[0]);
        r.W.MouseDown(At(chip), MouseButton.Left);
        r.W.MouseUp(At(chip), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new[] { kinds[1], kinds[0] }, r.T.Chosen);
    }

    [AvaloniaFact]
    public void UntickAllAndTheRowsAllAndNone()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        var kinds = r.P.Creatable().Select(c => c.Name).Take(4).ToList();
        r.P.Memory.Favourites.AddRange([kinds[0], kinds[1], "NotAThing_xyz"]);
        r.P.Memory.Recent.AddRange([kinds[2], kinds[3]]);
        Choose(r, kinds[3]);
        Click(r.P.FavAllButton);
        // Only kinds this world can place.
        Assert.Equal(new[] { kinds[3], kinds[0], kinds[1] }, r.T.Chosen);
        Click(r.P.FavNoneButton);
        Assert.Equal(new[] { kinds[3] }, r.T.Chosen);
        Click(r.P.RecentAllButton);
        Assert.Equal(new[] { kinds[3], kinds[2] }, r.T.Chosen);
        Click(r.P.RecentNoneButton);
        Assert.Empty(r.T.Chosen);
        Choose(r, kinds[0], kinds[1]);
        Click(r.P.UntickAllButton);
        Assert.Empty(r.T.Chosen);
        Assert.Empty(r.P.Memory.Chosen);
        Assert.Equal("Nothing to place yet: Choose kinds… below, or a preset.", r.P.Note.Text);
    }

    [AvaloniaFact]
    public void TheCategoriesFoldAndCountWhatIsTicked()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        var trees = r.P.Creatable().Where(c => c.Kind == ObjectKind.Trees).Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        var rocks = r.P.Creatable().Where(c => c.Kind == ObjectKind.Rocks).Select(c => c.Name).ToList();
        Assert.NotEmpty(trees);
        Assert.NotEmpty(rocks);
        Choose(r, trees[0]);
        Click(r.P.KindsButton);
        // Trees open (the default), the others folded: only the trees' boxes are made.
        Assert.True(r.P.Groups[ObjectKind.Trees].IsExpanded);
        Assert.False(r.P.Groups[ObjectKind.Rocks].IsExpanded);
        Assert.Equal(trees, Ticks(r).Select(b => (string)b.Tag!));
        string Count(ObjectKind k) => ((Grid)r.P.Groups[k].Header!).Children.OfType<TextBlock>().Last().Text!;
        Assert.Equal($"1 / {trees.Count}", Count(ObjectKind.Trees));
        Assert.Equal($"{rocks.Count}", Count(ObjectKind.Rocks));
        // Ticking in the list counts at once.
        Ticks(r).First(b => (string)b.Tag! == trees[^1]).IsChecked = true;
        Assert.Equal($"{(trees.Count > 1 ? 2 : 1)} / {trees.Count}", Count(ObjectKind.Trees));
        // Opening a category makes its boxes and is remembered.
        int changed = 0;
        r.P.OpenKindsChanged += () => changed++;
        r.P.Groups[ObjectKind.Rocks].IsExpanded = true;
        Assert.Contains(ObjectKind.Rocks, r.P.OpenKinds);
        Assert.Equal(1, changed);
        Assert.Equal(trees.Count + rocks.Count, Ticks(r).Count);
        r.P.Groups[ObjectKind.Trees].IsExpanded = false;
        Assert.DoesNotContain(ObjectKind.Trees, r.P.OpenKinds);
        Assert.Equal(2, changed);
        // Searching opens every category with a match, and is not remembered.
        r.P.Search.Text = rocks[0];
        Assert.All(r.P.Groups.Values, g => Assert.True(g.IsExpanded));
        r.P.Groups[ObjectKind.Rocks].IsExpanded = false;
        Assert.Contains(ObjectKind.Rocks, r.P.OpenKinds);
        Assert.Equal(2, changed);
        r.P.Search.Text = "no kind is called this";
        Assert.Empty(r.P.Groups);
        Assert.Contains("Nothing matches.", r.P.List.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
        // Back without a search: as remembered.
        r.P.Search.Text = "";
        Assert.False(r.P.Groups[ObjectKind.Trees].IsExpanded);
        Assert.True(r.P.Groups[ObjectKind.Rocks].IsExpanded);
    }

    [AvaloniaFact]
    public void TheOpenCategoriesAreRememberedBetweenRuns()
    {
        string file = Path.Combine(Path.GetTempPath(), $"vwe-prefs-open-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(file, "{\"place.openKinds\": [\"Rocks\", \"NotAKind\"]}");
            using (var r = new SharedPlaceWindow.PanelRun(prefs: Prefs.Load(file)))
            {
                Assert.Equal(new[] { ObjectKind.Rocks }, r.P.OpenKinds);
                Click(r.P.KindsButton);
                Assert.True(r.P.Groups[ObjectKind.Rocks].IsExpanded);
                Assert.False(r.P.Groups[ObjectKind.Trees].IsExpanded);
                r.P.Groups[ObjectKind.Trees].IsExpanded = true;
                r.W.Prefs.Flush();
            }
            Assert.Equal(new[] { "Rocks", "Trees" }, Prefs.Load(file).Get<string[]?>("place.openKinds", null));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
