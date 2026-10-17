using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's tools for the editor's own controls (ClaudeTools.Controls): the Workshop's Cut slider and Support check
/// found and set, blocked controls refused, only the View panel's while live with Auto on; and the helpers they use
/// (KindOf, LabelOf, ControlsOf, ValueOf, Set, Blocked).
/// </summary>
[Collection("World files")]
public class ClaudeToolsControlsTests
{
    // KindOf: each kind of control by its most specific type; others are not controls Claude sets.
    [AvaloniaFact]
    public void ControlsAreKnownByKind()
    {
        Assert.Equal("switch", ClaudeTools.KindOf(new CheckBox()));
        Assert.Equal("radio", ClaudeTools.KindOf(new RadioButton()));
        Assert.Equal("toggle", ClaudeTools.KindOf(new Avalonia.Controls.Primitives.ToggleButton()));
        Assert.Equal("button", ClaudeTools.KindOf(new Button()));
        Assert.Equal("dropdown", ClaudeTools.KindOf(new ComboBox()));
        Assert.Equal("slider", ClaudeTools.KindOf(new Slider()));
        Assert.Equal("number", ClaudeTools.KindOf(new NumericUpDown()));
        Assert.Equal("text", ClaudeTools.KindOf(new TextBox()));
        Assert.Null(ClaudeTools.KindOf(new TextBlock()));
    }

    // LabelOf: the content's text, a text box's placeholder, the text block just before it in its panel, else the
    // first sentence of its tip.
    [AvaloniaFact]
    public void ControlsAreLabelledByWhatTheUserReads()
    {
        Assert.Equal("Snap", ClaudeTools.LabelOf(new CheckBox { Content = "Snap" }));
        Assert.Equal("Find…", ClaudeTools.LabelOf(new TextBox { PlaceholderText = "Find…" }));
        var slider = new Slider();
        _ = new StackPanel { Children = { new TextBlock { Text = "Strength" }, slider } };
        Assert.Equal("Strength", ClaudeTools.LabelOf(slider));
        var tipped = new NumericUpDown().Tip("workshop.cut");
        Assert.Equal(((string)ToolTip.GetTip(tipped)!).Split(". ")[0], ClaudeTools.LabelOf(tipped));
    }

    // ControlsOf: tip keys as ids, labels otherwise, a number after an id already taken; ValueOf reads each kind.
    [AvaloniaFact]
    public void ControlsHaveIdsAndValues()
    {
        var root = new StackPanel
        {
            Children =
            {
                new CheckBox { Content = "Show water", IsChecked = true }.Tip("view.water"),
                new Button { Content = "Go" }, new Button { Content = "Go" },
                new ComboBox { ItemsSource = new[] { "Fast", "Sharp" }, SelectedIndex = 1 },
                new Slider { Minimum = 0, Maximum = 40, Value = 7 },
            },
        };
        var list = ClaudeTools.ControlsOf(root);
        Assert.Equal(["view.water", "~go", "~go#2", "~dropdown", "~slider"], list.Select(c => c.Id));
        Assert.Equal([true, null, null, "Sharp", 7.0], list.Select(c => ClaudeTools.ValueOf(c.Control)));
    }

    // Set: a switch on and off, a dropdown by text or number, a slider clamped, a text, a button clicked (its Click and
    // its Command), a switch clicked; wrong values are said. Blocked: the window's Save and Discard, blocked keys, and
    // blocked labels of untipped buttons.
    [AvaloniaFact]
    public void ControlsAreSetAndBlockedOnesRefused()
    {
        var w = new MainWindow(load: false);
        var t = new ClaudeTools(w);
        static ClaudeTools.Ctl C(string id, Control c) => new(id, ClaudeTools.KindOf(c)!, ClaudeTools.LabelOf(c), c);
        var box = new CheckBox();
        Assert.Null(t.Set(C("a", box), "on", false));
        Assert.True(box.IsChecked);
        Assert.StartsWith("a is a switch: give true or false", t.Set(C("a", box), "maybe", false));
        var combo = new ComboBox { ItemsSource = new[] { "Fast", "Sharp" } };
        Assert.Null(t.Set(C("b", combo), "sharp", false));
        Assert.Equal(1, combo.SelectedIndex);
        Assert.Null(t.Set(C("b", combo), "0", false));
        Assert.Equal(0, combo.SelectedIndex);
        Assert.Equal("b has no option \"Slow\": Fast, Sharp.", t.Set(C("b", combo), "Slow", false));
        var slider = new Slider { Minimum = 0, Maximum = 40 };
        Assert.Null(t.Set(C("c", slider), "99", false));
        Assert.Equal(40, slider.Value);
        // Not a number to set (NaN went through Math.Clamp), and a number too big for a decimal (it threw).
        Assert.StartsWith("c is a slider from 0 to 40: give a number", t.Set(C("c", slider), "NaN", false));
        Assert.StartsWith("c is a slider", t.Set(C("c", slider), "-Infinity", false));
        Assert.Equal(40, slider.Value);
        var number = new NumericUpDown { Minimum = 1, Maximum = 50 };
        Assert.Null(t.Set(C("g", number), "1e30", false));
        Assert.Equal(50m, number.Value);
        Assert.Null(t.Set(C("g", number), "-1e30", false));
        Assert.Equal(1m, number.Value);
        Assert.StartsWith("g takes a number", t.Set(C("g", number), "Infinity", false));
        var text = new TextBox();
        Assert.Null(t.Set(C("d", text), "Beech", true));
        Assert.Equal("Beech", text.Text);
        int clicks = 0;
        var command = new CountingCommand();
        var button = new Button { Content = "Go", Command = command };
        button.Click += (_, _) => clicks++;
        Assert.Null(t.Set(C("e", button), "click", false));
        Assert.Equal(1, clicks);
        Assert.Equal(1, command.Runs);
        Assert.Equal("e is a button: give click.", t.Set(C("e", button), "true", false));
        // A click on a switch changes it before its handlers run, as a pointer's does.
        var toggle = new CheckBox();
        bool? seen = null;
        toggle.Click += (_, _) => seen = toggle.IsChecked;
        int changes = 0;
        toggle.IsCheckedChanged += (_, _) => changes++;
        Assert.Null(t.Set(C("f", toggle), "click", false));
        Assert.True(toggle.IsChecked);
        Assert.True(seen);
        Assert.Equal(1, changes);

        Assert.NotNull(t.Blocked(C("save", w.SaveButton)));
        Assert.NotNull(t.Blocked(C("auto", new CheckBox().Tip("top.auto"))));
        Assert.NotNull(t.Blocked(C("export", new Button { Content = "Export" }.Tip("area.export"))));
        Assert.NotNull(t.Blocked(C("forget", new Button { Content = "forget" }.Tip("start.savedForget"))));
        Assert.NotNull(t.Blocked(C("plot", new Button { Content = "Open" }.Tip("dungeon.gen.openOnPlot"))));
        Assert.NotNull(t.Blocked(C("~delete", new Button { Content = "Delete" })));
        // The Blueprints rows' buttons that write or delete files and Select's Delete, by their key whatever their
        // label's language.
        string[] writing = ["select.delete", "blueprints.details", "blueprints.vbuild", "blueprints.vbuildOld", "blueprints.toHomestead", "blueprints.delete", "blueprints.deleteOld"];
        Assert.All(writing, k => Assert.NotNull(t.Blocked(C(k, new Button { Content = "Supprimer" }.Tip(k)))));
        Assert.Null(t.Blocked(C("blueprints.paste", new Button { Content = "Coller" }.Tip("blueprints.paste"))));
        Assert.Null(t.Blocked(C("~go", button)));
        Assert.StartsWith("save is not Claude's to use", t.Set(C("save", w.SaveButton), "click", false));
        w.CloseWithoutAsking();
    }

    // A step made by a control Claude sets is Claude's, "Claude: …" in the history, also when the handler makes it
    // after an await; the user's click of the same control makes a step without the prefix.
    [AvaloniaFact]
    public async Task StepsMadeByAControlClaudeSetsAreClaudes()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var t = new ClaudeTools(w);
        var s = TestScenes.Flat(1);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        var done = new TaskCompletionSource();
        bool keep = false;
        var button = new Button { Content = "Hill later" };
        button.Click += async (_, _) =>
        {
            await Task.Yield();
            s.Shape(32, 32, _ => 1, 3, 0, "a hill");
            // Marked another way each time, so each click is a step.
            keep = !keep;
            s.MarkResets([(0, 0)], keepBuildings: keep, ground: false, "Claude: a reset");
            done.TrySetResult();
        };
        w.GetLogicalDescendants().OfType<Panel>().First().Children.Add(button);
        Assert.Equal("~hill-later", ClaudeRun.J(await t.SetControl("~hill-later", "click")).GetProperty("id").GetString());
        await done.Task;
        // The ground step gets the prefix; the one that has it already is kept as it is.
        Assert.Equal(["Claude: a hill", "Claude: a reset"], s.UndoList.Select(c => c.Label));
        done = new TaskCompletionSource();
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await done.Task;
        Assert.Equal(["a hill", "Claude: a reset"], s.UndoList.Skip(2).Select(c => c.Label));
        w.CloseWithoutAsking();
    }

    // In the Workshop: the Cut slider and the Support check are listed and set as the user would; an unknown id is said.
    [AvaloniaFact]
    public async Task TheWorkshopsCutAndSupportCheckAreSet()
    {
        using var r = new ClaudeRun();
        await r.T.OpenWorkshop();
        var listed = ClaudeRun.J(await r.T.ListControls("workshop"));
        Assert.Contains(listed.GetProperty("controls").EnumerateArray(), c => c.GetProperty("id").GetString() == "workshop.cut");
        var cut = ClaudeRun.J(await r.T.SetControl("workshop.cut", "12"));
        Assert.Equal(12, cut.GetProperty("value").GetDouble());
        Assert.Equal(12, r.W.CutSlider.Value);
        await r.T.SetControl("workshop.support", "false");
        Assert.False(r.W.SupportBox.IsChecked);
        Assert.StartsWith("No control nothing.here now", await r.T.SetControl("nothing.here", "1"));
    }

    // Live with Auto on, what a control does would go to the game at once: a script's Run is refused, as every other
    // editing tool is; the View panel's switches still work.
    [AvaloniaFact]
    public async Task LiveWithAutoOnOnlyTheViewsControlsAreSet()
    {
        using var r = new ClaudeRun();
        await r.OpenLive();
        r.W.Settings.AutoApply = true;
        Assert.Contains("live with Auto on", await r.T.SetControl("script.code", "Ground.Raise(0, 0, 3);"));
        Assert.Contains("live with Auto on", await r.T.SetControl("script.run", "click"));
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        var water = ClaudeRun.J(await r.T.SetControl("view.water", "false"));
        Assert.False(water.GetProperty("value").GetBoolean());
        // Auto off: the script's controls are Claude's again.
        r.W.Settings.AutoApply = false;
        Assert.Equal("script.code", ClaudeRun.J(await r.T.SetControl("script.code", "Ground.Raise(0, 0, 3);")).GetProperty("id").GetString());
    }

    /// <summary>A command that can always run and counts its runs.</summary>
    private sealed class CountingCommand : ICommand
    {
        /// <summary>Not kept, as it is never raised: the command can always run.</summary>
#pragma warning disable S3237 // a listener need not be kept for an event that is never raised
        event EventHandler? ICommand.CanExecuteChanged { add { } remove { } }
#pragma warning restore S3237

        /// <summary>How many times the command ran.</summary>
        public int Runs { get; private set; }

        /// <inheritdoc/>
        public bool CanExecute(object? parameter) => true;

        /// <inheritdoc/>
        public void Execute(object? parameter) => Runs++;
    }
}
