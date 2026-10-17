using System.Numerics;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What the editor remembers between runs (Prefs, as the web editor kept it in the browser): the store's edge cases
/// (missing, broken, unwritable files, wrong types, the delayed write), the controls bound to it, and a window closed
/// and opened again with the View switches, the brush, the Area action, Select's options, the Shape preset and formula,
/// the right-hand panel, the clipboard and the map's place as they were. The tests' windows remember in memory only
/// unless given a file.
/// </summary>
public sealed class PrefsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-prefs-" + Guid.NewGuid().ToString("N")[..8]);
    private string File1 => Path.Combine(_dir, "prefs.json");

    public PrefsTests() => Directory.CreateDirectory(_dir);

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch
        {
            // Best effort: a folder that cannot be deleted is left for the OS to clean up.
        }
    }

    // ---- The store.

    [Fact]
    public void ValuesOfEveryTypeComeBackFromTheFile()
    {
        var p = Prefs.Load(File1);
        Assert.False(p.Has("a"));
        Assert.Equal(7, p.Get("a", 7));
        p.Set("a", true);
        p.Set("b", "text");
        p.Set("c", new[] { 1.5f, -2f, 3f });
        p.Set("d", new JsonObject { ["x"] = 1 });
        p.Flush();
        var q = Prefs.Load(File1);
        Assert.True(q.Get("a", false));
        Assert.Equal("text", q.Get("b", ""));
        Assert.Equal(new[] { 1.5f, -2f, 3f }, q.Get<float[]?>("c", null));
        Assert.True(q.TryGet("d", out JsonObject? d));
        Assert.Equal(1, (int)d!["x"]!);
        // Another type than the one kept: not there, the fallback.
        Assert.False(q.TryGet("b", out bool _));
        Assert.Equal(5, q.Get("b", 5));
        Assert.False(q.TryGet("a", out float[]? _));
        // Removed: gone from the file too.
        q.Remove("a");
        q.Remove("never there");
        q.Flush();
        Assert.False(Prefs.Load(File1).Has("a"));
        // Written sorted and readable.
        string text = File.ReadAllText(File1);
        Assert.True(text.IndexOf("\"b\"", StringComparison.Ordinal) < text.IndexOf("\"c\"", StringComparison.Ordinal));
        Assert.Contains("\n", text);
    }

    [Fact]
    public void ANullValueIsNotAValue()
    {
        File.WriteAllText(File1, "{\"a\": null, \"b\": 3}");
        var p = Prefs.Load(File1);
        Assert.True(p.Has("a"));
        Assert.False(p.TryGet("a", out string? _));
        Assert.Equal(3, p.Get("b", 0));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("42")]
    [InlineData("")]
    public void ABrokenFileMeansTheDefaults(string content)
    {
        File.WriteAllText(File1, content);
        var p = Prefs.Load(File1);
        Assert.False(p.Has("a"));
        // And it is written anew.
        p.Set("a", 1);
        p.Flush();
        Assert.Equal(1, Prefs.Load(File1).Get("a", 0));
    }

    [Fact]
    public void AFileThatCannotBeWrittenKeepsTheValuesInMemory()
    {
        Directory.CreateDirectory(File1);
        var p = Prefs.Load(File1);
        p.Set("a", 1);
        p.Flush();
        Assert.Equal(1, p.Get("a", 0));
        Assert.True(Directory.Exists(File1));
    }

    [Fact]
    public void AMissingFolderIsMade()
    {
        string path = Path.Combine(_dir, "new", "deeper", "prefs.json");
        var p = Prefs.Load(path);
        p.Set("a", 2);
        p.Flush();
        Assert.Equal(2, Prefs.Load(path).Get("a", 0));
    }

    [Fact]
    public async Task ChangesAreWrittenAMomentAfterTheLastOne()
    {
        var p = Prefs.Load(File1);
        for (int i = 0; i < 20; i++)
        {
            p.Set("n", i);
        }
        Assert.False(File.Exists(File1));
        for (int i = 0; i < 50 && !File.Exists(File1); i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        Assert.Equal(19, Prefs.Load(File1).Get("n", -1));
        // Removing writes too.
        p.Remove("n");
        for (int i = 0; i < 50 && Prefs.Load(File1).Has("n"); i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        Assert.False(Prefs.Load(File1).Has("n"));
    }

    [Fact]
    public void InMemoryPrefsAreNeverWritten()
    {
        var p = Prefs.InMemory();
        p.Set("a", 1);
        p.Remove("a");
        p.Set("b", 2);
        p.Flush();
        Assert.Equal(2, p.Get("b", 0));
        Assert.False(File.Exists(Prefs.DefaultPath) && File.ReadAllText(Prefs.DefaultPath).Contains("\"b\": 2"));
    }

    [Fact]
    public void TheDefaultFileIsInTheDataFolderUnlessOverridden()
    {
        string? old = Prefs.PathOverride;
        try
        {
            Prefs.PathOverride = null;
            Assert.Equal(Path.Combine(TerrainEditor.App.AppSettings.DataDir, "prefs.json"), Prefs.DefaultPath);
            Prefs.PathOverride = File1;
            Assert.Equal(File1, Prefs.DefaultPath);
            Prefs.Load().Set("x", 1);
        }
        finally
        {
            Prefs.PathOverride = old;
        }
    }

    // ---- Bound controls.

    [AvaloniaFact]
    public void ASwitchIsSetFromThePrefsAndRememberedWhenChanged()
    {
        var p = Prefs.Load(File1);
        var a = new CheckBox { IsChecked = true };
        p.Bind(a, "a");
        Assert.True(a.IsChecked);
        Assert.False(p.Has("a"));
        a.IsChecked = false;
        Assert.False(p.Get("a", true));
        var b = new CheckBox { IsChecked = true };
        p.Bind(b, "a");
        Assert.False(b.IsChecked);
    }

    [AvaloniaFact]
    public void AListIsRememberedByItsTextNotItsPlace()
    {
        var p = Prefs.Load(File1);
        var box = new ComboBox { ItemsSource = new[] { "one", "two", "three" }, SelectedIndex = 0 };
        p.Bind(box, "k");
        box.SelectedIndex = 2;
        Assert.Equal("three", p.Get("k", ""));
        // Another order, a longer list: still "three".
        var other = new ComboBox { ItemsSource = new List<string> { "zero", "three", "one" }, SelectedIndex = 0 };
        p.Bind(other, "k");
        Assert.Equal(1, other.SelectedIndex);
        // No longer in the list: left as it is.
        var gone = new ComboBox { ItemsSource = new[] { "a", "b" }, SelectedIndex = 1 };
        Assert.False(p.Restore(gone, "k"));
        Assert.Equal(1, gone.SelectedIndex);
        // An empty list, no list, nothing remembered.
        Assert.False(p.Restore(new ComboBox(), "k"));
        Assert.False(p.Restore(new ComboBox { ItemsSource = new[] { "three" } }, "nothing"));
        // Cleared: nothing remembered for it.
        other.SelectedIndex = -1;
        Assert.Equal("three", p.Get("k", ""));
    }

    // ---- A window closed and opened again.

    /// <summary>
    /// A shown main window remembering in the test's prefs file.
    /// </summary>
    private MainWindow Window()
    {
        var w = new MainWindow(load: false, prefs: Prefs.Load(File1)) { Width = 1200, Height = 900 };
        w.Show();
        return w;
    }

    /// <summary>
    /// A small 2 × 2 clipboard with a missing height and mixed weights, to see it remembered as it was.
    /// </summary>
    private static CopyData Clip() => new()
    {
        W = 2,
        H = 2,
        Rel = [0f, 1f, float.NaN, 2f],
        Wt = [1f, 1f, 0f, 0.5f],
        Pnt = [-1f, 0f, 0.5f, 1f],
        Objects = [new(Save.StableHash.Of("Beech1"), "Beech1", 1, 1, 0, new Vector3(0, 90, 0), 1, 1234)],
        Poly = [new(0, 0), new(2, 0), new(2, 2)],
        Name = "kept",
    };

    [AvaloniaFact]
    public void TheEditorsChoicesComeBackInTheNextWindow()
    {
        var w = Window();
        var kind = w.KindBoxes.Keys.First();
        w.KindBoxes[kind].IsChecked = !w.KindBoxes[kind].IsChecked;
        bool kindOn = w.KindBoxes[kind].IsChecked == true;
        w.WaterBox.IsChecked = false;
        var layer = w.OverlayBoxes.Keys.Last();
        w.OverlayBoxes[layer].IsChecked = !w.OverlayBoxes[layer].IsChecked;
        bool layerOn = w.OverlayBoxes[layer].IsChecked == true;
        w.SlopeBox.IsChecked = true;
        w.ContourBox.IsChecked = true;
        w.ContourStepBox.SelectedIndex = 3;
        w.Tools.ShapeBox.SelectedIndex = 2;
        w.Tools.FalloffBox.SelectedIndex = 4;
        w.AreaPanel.Choose(AreaPanel.Act.Regrow);
        w.SelectPanel.GroundBox.IsChecked = false;
        w.SelectPanel.SnapBox.IsChecked = false;
        w.ShapePanel.PresetBox.SelectedIndex = 1;
        w.ShowRight(w.HelpCard);
        w.Prefs.Flush();

        var v = Window();
        Assert.Equal(kindOn, v.KindBoxes[kind].IsChecked);
        Assert.Equal(kindOn, v.View.IsShown(kind));
        Assert.False(v.WaterBox.IsChecked);
        Assert.Equal(layerOn, v.OverlayBoxes[layer].IsChecked);
        Assert.Equal(layerOn, v.View.IsOverlayShown(layer));
        Assert.True(v.SlopeBox.IsChecked);
        Assert.True(v.View.SlopeColours);
        Assert.True(v.ContourBox.IsChecked);
        Assert.Equal(3, v.ContourStepBox.SelectedIndex);
        Assert.Equal(10, v.View.ContourStep);
        Assert.Equal(2, v.Tools.ShapeBox.SelectedIndex);
        Assert.Equal(4, v.Tools.FalloffBox.SelectedIndex);
        Assert.Equal(AreaPanel.Act.Regrow, v.AreaPanel.Current);
        Assert.False(v.SelectPanel.GroundBox.IsChecked);
        Assert.False(v.View.SelectTool.OnGround);
        Assert.False(v.View.SelectTool.SnapToPieces);
        Assert.Equal(1, v.ShapePanel.PresetBox.SelectedIndex);
        Assert.Equal(ShapePanel.Presets[1].Formula, v.ShapePanel.FormulaBox.Text);
        Assert.True(v.HelpCard.IsVisible);
    }

    [AvaloniaFact]
    public void AFormulaOfOnesOwnComesBack()
    {
        var w = Window();
        w.ShapePanel.FormulaBox.Text = "3 * (1 - d / r)";
        Assert.Equal(ShapePanel.Presets.Length, w.ShapePanel.PresetBox.SelectedIndex);
        w.Prefs.Flush();
        var v = Window();
        Assert.Equal(ShapePanel.Presets.Length, v.ShapePanel.PresetBox.SelectedIndex);
        Assert.Equal("3 * (1 - d / r)", v.ShapePanel.FormulaBox.Text);
        // Back to a preset: its own formula, the one of one's own still kept for later.
        v.ShapePanel.PresetBox.SelectedIndex = 0;
        v.Prefs.Flush();
        var u = Window();
        Assert.Equal(0, u.ShapePanel.PresetBox.SelectedIndex);
        Assert.Equal(ShapePanel.Presets[0].Formula, u.ShapePanel.FormulaBox.Text);
        Assert.Equal("3 * (1 - d / r)", u.Prefs.Get("shape.formula", ""));
    }

    [AvaloniaTheory]
    [InlineData("history")]
    [InlineData("none")]
    [InlineData("view")]
    [InlineData("something else")]
    public void TheRightHandPanelOpenComesBack(string which)
    {
        var p = Prefs.Load(File1);
        p.Set("panel.right", which);
        p.Flush();
        var v = Window();
        Assert.Equal(which == "history", v.History.Card.IsVisible);
        Assert.Equal(which is "view" or "something else", v.ViewButton.Classes.Contains("on"));
        Assert.False(v.HelpCard.IsVisible);
        // Read back as it was shown (an unknown one: the View panel); the Inspector and Blueprints are
        // not remembered: what was before stays.
        v.ShowRight(v.Inspector.Card);
        Assert.Equal(which == "something else" ? "view" : which, v.Prefs.Get("panel.right", ""));
        v.ShowRight(null);
        Assert.Equal("none", v.Prefs.Get("panel.right", ""));
    }

    [AvaloniaFact]
    public void TheClipboardComesBackWithoutItsSourceIds()
    {
        var w = Window();
        var c = Clip();
        w.View.Paste.Clip = c;
        w.View.Paste.Notify();
        // Notified again with the same copy (an offset changed): written once.
        w.View.Paste.Notify();
        w.Prefs.Flush();
        var v = Window();
        var back = v.View.Paste.Clip;
        Assert.NotNull(back);
        Assert.Equal(2, back!.W);
        Assert.True(float.IsNaN(back.Rel[2]));
        Assert.Equal("Beech1", Assert.Single(back.Objects).Name);
        Assert.Null(back.Objects[0].SourceId);
        Assert.Equal(90, back.Objects[0].Rotation.Y, 3);
        Assert.Equal(3, back.Poly.Count);
        Assert.Equal("kept", back.Name);
        // Pasting works from it at once (Ctrl+V needs no copy first).
        Assert.NotNull(v.View.Paste.Clip);
    }

    [AvaloniaFact]
    public void ABrokenClipboardIsForgotten()
    {
        var p = Prefs.Load(File1);
        p.Set("clipboard", new JsonObject { ["w"] = 2, ["h"] = 2 });
        p.Flush();
        var v = Window();
        Assert.Null(v.View.Paste.Clip);
        Assert.False(v.Prefs.Has("clipboard"));
        // Not even an object: ignored.
        p = Prefs.Load(File1);
        p.Set("clipboard", "text");
        p.Flush();
        Assert.Null(Window().View.Paste.Clip);
    }

    [AvaloniaFact]
    public void AnEntryNoLongerOfferedLeavesTheDefault()
    {
        var p = Prefs.Load(File1);
        p.Set("brush.shape", "Stamp: gone");
        p.Set("area.action", "An action that went away");
        p.Set("view.contourStep", "7");
        p.Set("view.slope", "not a bool");
        p.Flush();
        var v = Window();
        Assert.Equal(0, v.Tools.ShapeBox.SelectedIndex);
        Assert.Equal(AreaPanel.Act.Flatten, v.AreaPanel.Current);
        Assert.Equal(1, v.ContourStepBox.SelectedIndex);
        Assert.False(v.SlopeBox.IsChecked);
    }

    [AvaloniaFact]
    public void TestWindowsRememberNothingOnDisk()
    {
        var w = new MainWindow(load: false);
        w.SlopeBox.IsChecked = true;
        w.Prefs.Flush();
        Assert.False(new MainWindow(load: false).SlopeBox.IsChecked);
    }
}
