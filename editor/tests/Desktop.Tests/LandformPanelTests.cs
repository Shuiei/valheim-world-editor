using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Landform panel: one kind's rows shown at a time, each kind's outline and preview, what it says about the game's
/// ground limit, its presets named once across the kinds, and the Picture kind's pictures (the brushes' stamps).
/// </summary>
[Collection("DataDir")]
public class LandformPanelTests
{
    [AvaloniaFact]
    public void TheKindChosenShowsItsRowsAndSaysWhenItChanges()
    {
        var p = new LandformPanel();
        int changed = 0;
        p.Changed += () => changed++;
        Assert.Equal(LandformKind.Formula, p.Kind);
        Assert.True(p.Formula.Rows.IsVisible);
        Assert.False(p.Mountain.Rows.IsVisible);
        Assert.False(p.Picture.Rows.IsVisible);
        // Another kind's settings moved while it is not shown: nothing to follow.
        p.Mountain.SeedBox.Value = (p.Mountain.SeedBox.Value ?? 0) + 1;
        p.Picture.HeightBox.Value = 6;
        Assert.Equal(0, changed);
        p.Formula.RadiusSlider.Value = 30;
        Assert.Equal(1, changed);
        p.KindBox.SelectedIndex = (int)LandformKind.Mountain;
        Assert.Equal(2, changed);
        Assert.True(p.Mountain.Rows.IsVisible);
        Assert.False(p.Formula.Rows.IsVisible);
        Assert.Equal("A whole mountain made from a seed, up to 280 m around the click, with natural ridges and gullies.", p.HelpText.Text);
        p.KindBox.SelectedIndex = (int)LandformKind.Picture;
        Assert.True(p.Picture.Rows.IsVisible);
        Assert.False(p.Mountain.Rows.IsVisible);
        p.Picture.TurnSlider.Value = 45;
        Assert.Equal(4, changed);
    }

    [AvaloniaFact]
    public void EachKindSaysWhetherItObeysTheGamesLimit()
    {
        var p = new LandformPanel();
        Assert.StartsWith("Stops at the game's limit", p.LimitText.Text);
        p.ShowNoLimit(true);
        Assert.StartsWith("No limit is on", p.LimitText.Text);
        // A mountain goes past it whatever No limit says.
        p.KindBox.SelectedIndex = (int)LandformKind.Mountain;
        Assert.StartsWith("Always goes past", p.LimitText.Text);
        p.ShowNoLimit(false);
        Assert.StartsWith("Always goes past", p.LimitText.Text);
        p.KindBox.SelectedIndex = (int)LandformKind.Picture;
        Assert.StartsWith("Stops at the game's limit", p.LimitText.Text);
    }

    [AvaloniaFact]
    public void NoLimitSwitchedInTheWindowIsSaidInThePanel()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        Assert.StartsWith("Stops at the game's limit", w.LandformPanel.LimitText.Text);
        w.MaskPanel.NoLimitBox.IsChecked = true;
        Assert.StartsWith("No limit is on", w.LandformPanel.LimitText.Text);
        w.MaskPanel.NoLimitBox.IsChecked = false;
        Assert.StartsWith("Stops at the game's limit", w.LandformPanel.LimitText.Text);
    }

    [AvaloniaFact]
    public void NoPresetNameIsGivenTwiceAcrossTheKinds()
    {
        var p = new LandformPanel();
        var names = new[] { p.Formula.PresetBox, p.Mountain.PresetBox, p.Picture.PresetBox }.SelectMany(b => b.Items.Cast<string>()).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // The flat-topped hill: a Plateau as a formula, a Mesa as a mountain; the mesa and crater rim pictures, which
        // the formulas make (Plateau, Moat), are brush shapes only.
        Assert.Contains("Plateau", p.Formula.PresetBox.Items.Cast<string>());
        Assert.Contains("Mesa", p.Mountain.PresetBox.Items.Cast<string>());
        Assert.Equal(["Rough hill", "Dunes", "Rocky ground"], p.Picture.PresetBox.Items.Cast<string>());
        Assert.Equal(Stamps.BuiltIn.Length - 2, Stamps.Landforms(Stamps.BuiltIn).Count);
    }

    [AvaloniaFact]
    public void TheFormulasPreviewIsItsShapeWithinTheRadius()
    {
        var p = new LandformPanel();
        var noise = new Noise(3);
        p.Formula.RadiusSlider.Value = 10;
        p.Formula.HeightBox.Value = 4;
        // The Cone: h at the middle, half of it halfway, nothing beyond the radius.
        p.Formula.PresetBox.SelectedIndex = 1;
        var ghost = p.Ghost(() => noise);
        Assert.Equal(10, ghost.Reach);
        Assert.False(ghost.MustFit);
        Assert.Null(ghost.Square);
        Assert.Equal(4, ghost.Shape!(0, 0), 3);
        Assert.Equal(2, ghost.Shape(5, 0), 3);
        Assert.Equal(0, ghost.Shape(8, 8), 3);
        // A hollow is negative; a point the formula gives no number for adds nothing.
        p.Formula.FormulaBox.Text = "x > 0 ? 0 / 0 : -h";
        ghost = p.Ghost(() => noise);
        Assert.Equal(-4, ghost.Shape!(-1, 0), 3);
        Assert.Equal(0, ghost.Shape(1, 0), 3);
        // A formula with a mistake: no preview, the outline stays.
        p.Formula.FormulaBox.Text = "h * ";
        ghost = p.Ghost(() => noise);
        Assert.Null(ghost.Shape);
        Assert.Equal(40, ghost.Outline(40).Count);
        Assert.All(ghost.Outline(40), o => Assert.Equal(10, MathF.Sqrt(o.X * o.X + o.Z * o.Z), 3));
    }

    [AvaloniaFact]
    public void ThePicturesPreviewIsThePictureTimesTheHeightInItsSquare()
    {
        var p = new LandformPanel();
        p.KindBox.SelectedIndex = (int)LandformKind.Picture;
        p.Picture.RadiusSlider.Value = 20;
        p.Picture.HeightBox.Value = -5;
        var ghost = p.Ghost(() => new Noise(1));
        float middle = Stamps.Sample(Stamps.BuiltIn[0].Data, 0, 0);
        Assert.Equal(-5 * middle, ghost.Shape!(0, 0), 3);
        Assert.Equal(0, ghost.Shape(25, 0), 3);
        // The outline is the picture's square (its side twice the radius), and the preview reaches its corners.
        Assert.Equal(20 * 1.42f, ghost.Reach, 2);
        var outline = ghost.Outline(64);
        Assert.Equal(20, outline.Max(o => MathF.Max(MathF.Abs(o.X), MathF.Abs(o.Z))), 3);
        // Turned an eighth of a turn, the square's corners point east and north.
        p.Picture.TurnSlider.Value = 45;
        Assert.Equal(20 * MathF.Sqrt(2), p.Ghost(() => new Noise(1)).Outline(64).Max(o => o.X), 1);
        // The brush it is put in with is the picture's own, not the sculpt brushes'.
        var brush = p.Picture.Brush;
        Assert.Equal(BrushShape.Stamp, brush.Shape);
        Assert.Same(Stamps.BuiltIn[0].Data, brush.StampData);
        Assert.Equal(20, brush.Radius);
        Assert.Equal(45, brush.Turn);
    }

    [AvaloniaFact]
    public void APictureLoadedForTheLandformIsChosenThereAndForgottenEverywhere()
    {
        using var kept = new KeptStamps();
        var w = TestWindows.Editing(TestScenes.Flat(2));
        var picture = w.LandformPanel.Picture;
        var t = w.Tools;
        t.ShapeBox.SelectedIndex = 1;
        int before = picture.PresetBox.ItemCount;
        Assert.False(picture.ForgetButton.IsVisible);
        // Loaded for the Landform tool: one of its pictures, chosen there; a brush shape too, but not the brush's.
        var disc = t.AddStamp("Disc", [.. Enumerable.Repeat(1f, Stamps.Size * Stamps.Size)], choose: false);
        picture.Choose(disc);
        Assert.Equal(before + 1, picture.PresetBox.ItemCount);
        Assert.Same(disc, picture.Stamp);
        Assert.Equal("Disc", picture.PresetBox.SelectedItem);
        Assert.True(picture.ForgetButton.IsVisible);
        Assert.Equal(1, t.ShapeBox.SelectedIndex);
        Assert.Contains("Stamp: Disc", t.ShapeBox.Items.Cast<string>());
        // Forgotten from the Landform panel: gone from both, the first picture chosen again.
        picture.ForgetButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(before, picture.PresetBox.ItemCount);
        Assert.Same(Stamps.BuiltIn[0], picture.Stamp);
        Assert.DoesNotContain("Stamp: Disc", t.ShapeBox.Items.Cast<string>());
        Assert.Equal(1, t.ShapeBox.SelectedIndex);
        Assert.DoesNotContain(Stamps.LoadKept(), k => k.Label == "Disc");
        // A built-in picture cannot be forgotten.
        Assert.False(t.ForgetStamp(Stamps.BuiltIn[0]));
        // Loaded as a brush shape, then forgotten while it is the brush's shape: the brush goes back to a circle.
        var ring = t.AddStamp("Ring", new float[Stamps.Size * Stamps.Size]);
        Assert.Equal("Stamp: Ring", t.ShapeBox.SelectedItem);
        Assert.Contains("Ring", picture.PresetBox.Items.Cast<string>());
        Assert.True(t.ForgetStamp(ring));
        Assert.Equal(0, t.ShapeBox.SelectedIndex);
    }

    [AvaloniaFact]
    public void TheLandformKeyOpensThePanelWithTheKindLastChosen()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        w.LandformPanel.KindBox.SelectedIndex = (int)LandformKind.Picture;
        Assert.True(w.Tools.Key("G"));
        Assert.Equal(ToolMode.Landform, w.Tools.Mode);
        Assert.True(w.LandformPanel.Card.IsVisible);
        Assert.True(w.MaskPanel.Card.IsVisible);
        Assert.Equal(LandformKind.Picture, w.LandformPanel.Kind);
        Assert.Equal(new HelpTarget("landform.md"), HelpTopics.Tool(ToolMode.Landform));
    }

    [Fact]
    public void ThePreviewDrawsHollowsTooAndLeavesOutWhatHardlyMoves()
    {
        var scene = TestScenes.Flat(2).Scene;
        int c = (scene.W - 1) / 2;
        float ground = scene.Heights[c * scene.W + c];
        // A bowl 3 m deep: drawn below the ground.
        float[] bowl = GlView.MountainMesh(scene, c, c, 10, (x, z) => x * x + z * z < 64 ? -3 : 0, 0.05f);
        Assert.NotEmpty(bowl);
        Assert.True(Enumerable.Range(0, bowl.Length / 3).Min(i => bowl[i * 3 + 1]) < ground - 2);
        // 20 cm of relief: left out by the mountains' half metre, drawn with the smaller landforms' 5 cm.
        Assert.Empty(GlView.MountainMesh(scene, c, c, 10, (_, _) => 0.2f));
        Assert.NotEmpty(GlView.MountainMesh(scene, c, c, 10, (_, _) => 0.2f, 0.05f));
    }

    [Fact]
    public void AFormulasShapeIsTheSameForThePreviewAndTheGround()
    {
        var s = TestScenes.Flat(2);
        var f = Formula.Compile("h * max(0, 1 - d / r)", ["x", "z", "d", "r", "h"]);
        var at = EditSession.ShapeOf(f, 10, 5, () => s.Brush.Noise);
        Assert.Equal(5, at(0, 0), 3);
        Assert.Equal(2.5, at(3, 4), 3);
        // A formula that fails at a point gives no number there.
        var failing = EditSession.ShapeOf(e => e.Vars["x"] > 0 ? throw new InvalidOperationException("east") : 2, 6, 0, () => s.Brush.Noise);
        Assert.Equal(2, failing(-1, 0), 3);
        Assert.True(double.IsNaN(failing(1, 0)));
        s.Shape(64, 64, f, 10, 5, "cone");
        Assert.Equal(35, s.Scene.Heights[64 * s.Scene.W + 64], 3);
        Assert.Equal(32.5, s.Scene.Heights[68 * s.Scene.W + 67], 3);
    }
}
