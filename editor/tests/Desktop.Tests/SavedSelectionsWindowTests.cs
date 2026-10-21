using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

using static TerrainEditor.Desktop.Tests.SavedSelectionsData;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select panel's saved selections: Keep, Select, Add and Forget, the naming, each world's own, the buttons, and a
/// selection found again after the ids changed.
/// </summary>
[Collection("Saved selections")]
public sealed class SavedSelectionsWindowTests : SavedSelectionsFolder
{
    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area with these objects, in the Select tool, its panel's messages
    /// collected and its questions answered (world "World one", the name given, yes).
    /// </summary>
    private static (MainWindow W, EditSession S, List<string> Messages) Window(params WorldScene.Thing[] things)
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        var s = TestScenes.Flat(2, things);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        var messages = new List<string>();
        w.SelectPanel.Message += m => messages.Add(m.Shown);
        w.SelectPanel.WorldName = () => "World one";
        w.SelectPanel.AskName = Task.FromResult<string?>;
        w.SelectPanel.Confirm = _ => Task.FromResult(true);
        return (w, s, messages);
    }

    /// <summary>
    /// Raises the button's click (no posted work run).
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public async Task KeepSelectAddAndForgetASelection()
    {
        var (w, s, messages) = Window(T(1, Beech, 40, 30, 40), T(2, Beech, 44, 30, 40), T(3, Wall, 48, 30, 40));
        var p = w.SelectPanel;
        p.FillSaved();
        Assert.Equal("No saved selections", p.SavedBox.PlaceholderText);
        Assert.Equal(0, p.SavedBox.ItemCount);
        // Nothing selected: nothing to keep.
        await p.Keep();
        Assert.Equal("Select something first.", messages[^1]);
        // Kept under the name offered (Selection 1), then under a name typed.
        w.View.Select([0, 1]);
        await p.Keep();
        Assert.Equal("Kept the selection “Selection 1” (2 object(s)). It can be picked again here, also after saving the world.", messages[^1]);
        Assert.Equal("Selection 1 (2)", p.SavedBox.SelectedItem);
        Assert.Equal("Saved selections…", p.SavedBox.PlaceholderText);
        w.View.Select([2]);
        p.AskName = n => Task.FromResult<string?>("  The wall  ");
        await p.Keep();
        Assert.Equal(["Selection 1 (2)", "The wall (1)"], p.SavedBox.Items.Cast<string>());
        Assert.Equal(1, p.SavedBox.SelectedIndex);
        // Select it again: only the wall; Add: the trees too.
        w.View.Select([]);
        p.UseSaved(false);
        Assert.Equal([2], w.View.Selected);
        Assert.Equal("Selected 1 object(s) of “The wall”.", messages[^1]);
        p.SavedBox.SelectedIndex = 0;
        p.UseSaved(true);
        Assert.Equal([0, 1, 2], w.View.Selected.OrderBy(i => i));
        // One tree deleted since: said, the rest selected.
        s.Delete([1]);
        p.UseSaved(false);
        Assert.Equal([0], w.View.Selected);
        Assert.Equal("Selected 1 object(s) of “Selection 1”; 1 are not in this area or no longer where they were.", messages[^1]);
        // Forget, after asking (no: kept).
        p.Confirm = _ => Task.FromResult(false);
        await p.Forget();
        Assert.Equal(2, SavedSelections.For("World one").Count);
        string? asked = null;
        p.Confirm = t => { asked = t; return Task.FromResult(true); };
        await p.Forget();
        Assert.Equal("Forget the selection “Selection 1”?", asked);
        Assert.Equal(["The wall (1)"], p.SavedBox.Items.Cast<string>());
        Assert.Equal(-1, p.SavedBox.SelectedIndex);
        // Nothing chosen: the buttons do nothing.
        int count = messages.Count;
        p.UseSaved(false);
        await p.Forget();
        Assert.Equal(count, messages.Count);
    }

    [AvaloniaFact]
    public async Task NamingCancelledOrBlankKeepsNothing()
    {
        var (w, _, messages) = Window(T(1, Beech, 40, 30, 40));
        var p = w.SelectPanel;
        w.View.Select([0]);
        p.AskName = _ => Task.FromResult<string?>(null);
        await p.Keep();
        p.AskName = _ => Task.FromResult<string?>("   ");
        await p.Keep();
        Assert.Empty(SavedSelections.For("World one"));
        Assert.Empty(messages);
    }

    [AvaloniaFact]
    public async Task SelectionsBelongToTheirWorld()
    {
        var (w, _, messages) = Window(T(1, Beech, 40, 30, 40));
        var p = w.SelectPanel;
        w.View.Select([0]);
        await p.Keep();
        p.WorldName = () => "Another world";
        p.FillSaved();
        Assert.Equal(0, p.SavedBox.ItemCount);
        Assert.False(p.LoadButton.IsEffectivelyVisible);
        // No world (nothing open): nothing kept.
        p.WorldName = () => null;
        await p.Keep();
        Assert.Equal("Select something first.", messages[^1]);
        p.FillSaved();
        Assert.Equal(0, p.SavedBox.ItemCount);
    }

    [AvaloniaFact]
    public async Task TheButtonsDoWhatTheySay()
    {
        var (w, _, _) = Window(T(1, Beech, 40, 30, 40), T(2, Wall, 44, 30, 40));
        var p = w.SelectPanel;
        w.View.Select([1]);
        Click(p.KeepButton);
        await Task.Delay(20);
        Assert.Single(SavedSelections.For("World one"));
        // The row of buttons shows once one is chosen.
        Assert.True(p.LoadButton.IsVisible || p.LoadButton.Parent is Control { IsVisible: true });
        w.View.Select([0]);
        Click(p.LoadButton);
        Assert.Equal([1], w.View.Selected);
        w.View.Select([0]);
        Click(p.AddSavedButton);
        Assert.Equal([0, 1], w.View.Selected.OrderBy(i => i));
        Click(p.ForgetButton);
        await Task.Delay(20);
        Assert.Empty(SavedSelections.For("World one"));
        Assert.False(((Control)p.LoadButton.Parent!).IsVisible);
    }

    [AvaloniaFact]
    public async Task ASelectionIsFoundAgainAfterTheIdsChanged()
    {
        // Kept in one area; the "saved world" has the same objects under new ids, and moved ones.
        var (w, _, _) = Window(T(1, Beech, 40, 30, 40), T(2, Wall, 44, 31, 40));
        w.View.Select([0, 1]);
        await w.SelectPanel.Keep();
        var (w2, _, messages2) = Window(T(900, Wall, 44, 31.1f, 40.02f), T(901, Beech, 41, 30, 40));
        w2.SelectPanel.FillSaved();
        w2.SelectPanel.SavedBox.SelectedIndex = 0;
        w2.SelectPanel.UseSaved(false);
        Assert.Equal([0], w2.View.Selected);
        Assert.Equal("Selected 1 object(s) of “Selection 1”; 1 are not in this area or no longer where they were.", messages2[^1]);
        // Selecting switches to the Select tool.
        w2.Tools.ChooseMode(ToolMode.Measure);
        w2.SelectPanel.UseSaved(false);
        Assert.Equal(ToolMode.Select, w2.Tools.Mode);
    }
}
