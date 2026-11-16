using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The rows of the brush settings more than one tool uses: Naturalize's Bumps, Bump size and New pattern (the
/// Naturalize brush, the Area and Path tools' Naturalize, the Path tool's Natural look) and Erode's Rest angle (the
/// Erode brush, the Area and Path tools' Erode). Each panel shows its own copy of a row next to the action that uses
/// it; every copy sets the same value of the one brush and shows what another copy set.
/// </summary>
internal static class BrushRows
{
    /// <summary>Bumps: how tall the natural bumps are, 0.2 to 4 m.</summary>
    /// <param name="b">The brush.</param>
    /// <param name="shown">Where the value is shown.</param>
    /// <returns>The slider.</returns>
    public static Slider Bumps(Brush b, TextBlock shown) =>
        Bound(b, 0.2, 4, 0.1, x => x.NoiseAmp, (x, v) => x.NoiseAmp = v, shown, v => $"{v:0.0} m").Tip("natural.amp");

    /// <summary>Bump size: how wide the natural bumps are, 4 to 60 m.</summary>
    /// <param name="b">The brush.</param>
    /// <param name="shown">Where the value is shown.</param>
    /// <returns>The slider.</returns>
    public static Slider BumpSize(Brush b, TextBlock shown) =>
        Bound(b, 4, 60, 1, x => x.NoiseSize, (x, v) => x.NoiseSize = v, shown, v => $"{v:0} m").Tip("natural.size");

    /// <summary>Rest angle: the steepest slope that stays put under thermal erosion, 10 to 60 degrees.</summary>
    /// <param name="b">The brush.</param>
    /// <param name="shown">Where the value is shown.</param>
    /// <returns>The slider.</returns>
    public static Slider RestAngle(Brush b, TextBlock shown) =>
        Bound(b, 10, 60, 1, x => x.RestAngle, (x, v) => x.RestAngle = v, shown, v => $"{v:0}°").Tip("erode.rest");

    /// <summary>New pattern: another random pattern for the bumps, said on the status bar.</summary>
    /// <param name="b">The brush.</param>
    /// <param name="say">Shows a line on the status bar.</param>
    /// <returns>The button.</returns>
    public static Button NewPattern(Brush b, Action<Said> say)
    {
        var button = new Button { Content = Lang.T("brush.label.newPattern"), FontSize = 12 }.Tip("natural.seed");
        button.Click += (_, _) =>
        {
            b.NewPattern();
            say(Said.Of("brush.msg.newPattern"));
        };
        return button;
    }

    /// <summary>The Naturalize rows: Bumps, Bump size and New pattern.</summary>
    /// <param name="b">The brush.</param>
    /// <param name="newPattern">The New pattern button (<see cref="NewPattern"/>).</param>
    /// <param name="columns">The rows' column widths.</param>
    /// <returns>The rows, one under the other.</returns>
    public static StackPanel Natural(Brush b, Button newPattern, string columns = "70,*,46")
    {
        TextBlock amp = new(), size = new();
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Ui.Row(Lang.T("brush.label.bumps"), Bumps(b, amp), amp, columns),
                Ui.Row(Lang.T("brush.label.bumpSize"), BumpSize(b, size), size, columns),
                newPattern,
            },
        };
    }

    /// <summary>The Rest angle row.</summary>
    /// <param name="b">The brush.</param>
    /// <param name="slider">Gets the row's slider.</param>
    /// <param name="columns">The row's column widths.</param>
    /// <returns>The row.</returns>
    public static Grid Rest(Brush b, out Slider slider, string columns = "70,*,46")
    {
        var shown = new TextBlock();
        slider = RestAngle(b, shown);
        return Ui.Row(Lang.T("brush.label.restAngle"), slider, shown, columns);
    }

    /// <summary>
    /// A slider for one of the brush's values: moving it sets the value and tells the other copies, and it follows the
    /// value when another copy sets it.
    /// </summary>
    private static Slider Bound(Brush b, double min, double max, double step, Func<Brush, float> get, Action<Brush, float> set, TextBlock shown, Func<double, string> format)
    {
        var slider = Ui.Slide(min, max, step, get(b), shown, format, v =>
        {
            if (get(b) != (float)v)
            {
                set(b, (float)v);
                b.NotifySettings();
            }
        });
        b.SettingsChanged += () =>
        {
            if ((float)slider.Value != get(b))
            {
                slider.Value = get(b);
            }
        };
        return slider;
    }
}
