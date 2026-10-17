using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool's mouse and keys in another language (the made-up one: PseudoLang): every status message they give
/// and the History steps of placing are shown in the language, with their English kept for Claude; what was built
/// says what it snapped to in one whole sentence for each way of snapping.
/// </summary>
[Collection("DataDir")]
public sealed class PlaceInputLangTests
{
    [AvaloniaFact]
    public void EveryMessageAndStepIsShownInTheLanguageAndReadInEnglish()
    {
        using var pseudo = new PseudoLang();
        var r = new PlaceInputTests.Rig(PlaceTool.Modes.Brush);
        // Brush: picking (nothing there, cancelled), a height picked, the lift, the elevation keys, a click placing
        // and a Shift click erasing.
        r.In.PickOnce = (_, _) => { };
        r.Click(10, 10);
        r.In.PickOnce = (_, _) => { };
        Assert.True(r.Key(Key.Escape));
        r.Click(40, 40, alt: true);
        r.In.Lift(0.5f);
        r.In.Lift(-0.5f);
        Assert.True(r.Key(Key.PageUp));
        r.Click(64, 64);
        Assert.StartsWith(PseudoLang.Open.ToString(), r.S.UndoLabel);
        r.Click(64, 64, shift: true);
        // Zones and grids turned, and placed, with nothing drawn.
        r.T.Mode = PlaceTool.Modes.Zone;
        Assert.True(r.Key(Key.OemPeriod));
        r.In.PlaceShape();
        r.T.Mode = PlaceTool.Modes.Grid;
        Assert.True(r.Key(Key.OemPeriod));
        r.In.PlaceShape();
        // Lines: placed with nothing drawn, a circle and a rectangle clicked, then a line placed.
        r.T.Mode = PlaceTool.Modes.Line;
        r.In.PlaceShape();
        r.T.LineShape = PlaceTool.LineShapes.Circle;
        r.Click(64, 64);
        r.T.LineShape = PlaceTool.LineShapes.Rect;
        r.Click(64, 64);
        r.T.LineShape = PlaceTool.LineShapes.Points;
        r.Click(40, 64);
        r.Click(60, 64);
        int shown = r.In.Shown.Length;
        Assert.True(r.Key(Key.Enter));
        Assert.Equal(PseudoLang.Wrap($"Placed {shown} along a line"), r.S.UndoLabel);

        Assert.All(r.Told, t =>
        {
            Assert.StartsWith(PseudoLang.Open.ToString(), t.Shown);
            Assert.DoesNotContain(PseudoLang.Open, t.English);
        });
        string[] english = [.. r.Told.Select(t => t.English)];
        Assert.Contains(english, e => e.StartsWith("Nothing picked", StringComparison.Ordinal));
        Assert.Contains("Picking cancelled.", english);
        Assert.Contains(english, e => e.StartsWith("Elevation: everything at", StringComparison.Ordinal));
        Assert.Contains(english, e => e.StartsWith("Lift: +0.5 m", StringComparison.Ordinal));
        Assert.Contains("Lift: none (where the cursor points).", english);
        Assert.Contains(english, e => e.StartsWith("Placed ", StringComparison.Ordinal) && e.EndsWith("Save writes them to the world.", StringComparison.Ordinal));
        Assert.Contains(english, e => e.StartsWith("Removed ", StringComparison.Ordinal));
        Assert.Contains("Draw a zone first, then turn it.", english);
        Assert.Contains("Draw a zone first (click points around it, or drag).", english);
        Assert.Contains("Drag a box first, then turn it.", english);
        Assert.Contains("Drag a box on the ground first.", english);
        Assert.Contains("Draw a line first (click points on the ground).", english);
        Assert.Contains("Press at the centre and drag out to the size.", english);
        Assert.Contains("Press at a corner and drag to the opposite one.", english);
        Assert.Equal($"Placed {shown} object(s). Ctrl+Z removes them.", english[^1]);
    }

    [Fact]
    public void WhatWasBuiltSaysWhatItSnappedTo()
    {
        using var pseudo = new PseudoLang();
        (PlaceTool.SnapWays Way, string Name)?[] snaps =
            [null, (PlaceTool.SnapWays.OnTop, "Wood Wall"), (PlaceTool.SnapWays.Beside, "Wood Wall"), (PlaceTool.SnapWays.To, "Wood Wall")];
        string[] english =
        [
            "Placed Wood Beam. Ctrl+Z takes it back.",
            "Placed Wood Beam on top of Wood Wall. Ctrl+Z takes it back.",
            "Placed Wood Beam beside Wood Wall. Ctrl+Z takes it back.",
            "Placed Wood Beam snapped to Wood Wall. Ctrl+Z takes it back.",
        ];
        for (int i = 0; i < snaps.Length; i++)
        {
            var said = PlaceInput.Built("Wood Beam", snaps[i]);
            Assert.Equal(english[i], said.English);
            Assert.Equal(PseudoLang.Wrap(english[i]), said.Shown);
        }
    }
}
