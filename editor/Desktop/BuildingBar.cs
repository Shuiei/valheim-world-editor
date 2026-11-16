using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The building bar: the settings every tool that puts pieces down or moves them shares, in one bar beside the tool
/// rail. How pieces snap (Auto, Manual or Free), the grid for those not snapped, the step , and . turn by, the step
/// PgUp, PgDn and Ctrl + wheel lift by, and who the pieces are built by. One state for Build, Select and Paste: each
/// shows the parts it uses. Shift always makes a step finer.
/// </summary>
public sealed class BuildingBar
{
    /// <summary>The tool the bar is shown for: it shows the parts that tool uses.</summary>
    public enum Uses
    {
        /// <summary>No tool that builds: the bar is hidden.</summary>
        None,
        /// <summary>Build: every part.</summary>
        Build,
        /// <summary>Select: snapping (on or off), the grid, the steps and the builder.</summary>
        Select,
        /// <summary>Paste: the grid, the steps, the lift and the builder.</summary>
        Paste,
    }

    /// <summary>The grid steps behind <see cref="GridBox"/>'s entries, in metres (0: off).</summary>
    internal static readonly float[] Grids = [0, 0.5f, 1, 2];

    /// <summary>The turn steps behind <see cref="TurnBox"/>'s entries, in degrees.</summary>
    internal static readonly float[] Turns = [1, 15, 22.5f, 45, 90];

    /// <summary>The lift steps behind <see cref="LiftBox"/>'s entries, in metres.</summary>
    internal static readonly float[] Lifts = [0.1f, 0.25f, 0.5f, 1];

    /// <summary>The turn Shift gives, in degrees, whatever the step (never more than the step).</summary>
    public const float FineTurn = 1;

    /// <summary>The bar itself, hidden until a tool that builds is chosen.</summary>
    public Border Card { get; }

    /// <summary>How the piece snaps: Auto (as the hammer), Manual (one snap point, Q and E) or Free; one button each, tagged with its mode.</summary>
    internal StackPanel SnapModes { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Snapping by hand: which of the piece's snap points meets the others ("Point 2 of 8").</summary>
    internal TextBlock SnapPointText { get; } = new() { FontSize = 11.5, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };

    /// <summary>The grid step for pieces not snapped (Off, 0.5, 1 or 2 m).</summary>
    internal ComboBox GridBox { get; } = new() { ItemsSource = new[] { Lang.T("build.grid.off"), "0.5 m", "1 m", "2 m" }, SelectedIndex = 0, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The step , and . turn by. The game turns the hammer's piece by 22.5° a step.</summary>
    internal ComboBox TurnBox { get; } = new() { ItemsSource = new[] { "1°", "15°", "22.5°", "45°", "90°" }, SelectedIndex = 2, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The step PgUp, PgDn and Ctrl + wheel lift by.</summary>
    internal ComboBox LiftBox { get; } = new() { ItemsSource = new[] { "0.1 m", "0.25 m", "0.5 m", "1 m" }, SelectedIndex = 2, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The lift the piece (Build) or the paste has now (see <see cref="ShowLift"/>).</summary>
    internal TextBlock LiftText { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Takes the lift back to none.</summary>
    internal Button LiftReset { get; } = new() { Content = Lang.T("build.label.reset"), FontSize = 11, Padding = new Thickness(6, 1), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Built by: who new pieces are written as built by (the window fills it for the open world).</summary>
    internal ComboBox BuilderBox { get; } = new() { FontSize = 12, MinWidth = 150, MaxWidth = 220, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The snapping chosen.</summary>
    public PlaceTool.SnapModes SnapMode { get; private set; }

    /// <summary>Whether pieces snap to the pieces around (Auto or Manual).</summary>
    public bool Snaps => SnapMode != PlaceTool.SnapModes.Free;

    /// <summary>The grid step, in metres (0: off).</summary>
    public float GridStep => Grids[Math.Clamp(GridBox.SelectedIndex, 0, Grids.Length - 1)];

    /// <summary>The step , and . turn by, in degrees.</summary>
    public float TurnStep => Turns[Math.Clamp(TurnBox.SelectedIndex, 0, Turns.Length - 1)];

    /// <summary>The step PgUp, PgDn and Ctrl + wheel lift by, in metres.</summary>
    public float LiftStep => Lifts[Math.Clamp(LiftBox.SelectedIndex, 0, Lifts.Length - 1)];

    /// <summary>The turn of one press of , or .: the step, or with Shift the fine turn (never more than the step).</summary>
    /// <param name="shift">Shift is held: finer.</param>
    public float TurnBy(bool shift) => shift ? MathF.Min(FineTurn, TurnStep) : TurnStep;

    /// <summary>The lift of one press of PgUp or PgDn, or one notch of Ctrl + wheel: the step, or with Shift a fifth of it.</summary>
    /// <param name="shift">Shift is held: finer.</param>
    public float LiftBy(bool shift) => shift ? MathF.Round(LiftStep / 5, 2) : LiftStep;

    /// <summary>The tool the bar is shown for.</summary>
    public Uses Use { get; private set; }

    /// <summary>A setting changed (the snapping, the grid or a step): the tools take it.</summary>
    public event Action? Changed;

    /// <summary>Reset was clicked: the tool in use takes its lift back to none.</summary>
    public event Action? LiftResetAsked;

    /// <summary>Where the settings are kept between runs.</summary>
    private Prefs _prefs = Prefs.InMemory();

    /// <summary>The snapping's part of the bar (Build and Select), with the line after it.</summary>
    private readonly Control _snapPart, _snapSep;

    /// <summary>The lift now and its Reset (Build and Paste: Select lifts the selection itself).</summary>
    private readonly Control _liftNow;

    /// <summary>Built by, with the line before it (a world only: a blueprint keeps no builder).</summary>
    private readonly Control _builderPart, _builderSep;

    /// <summary>The bar's parts, wrapping onto a second row when too wide for the room it has (<see cref="Fit"/>).</summary>
    private readonly WrapPanel _parts;

    /// <summary>The least width the bar is squeezed to, in pixels: its widest part fits.</summary>
    internal const double MinWidth = 300;

    /// <summary>The bar may be this wide (card and all) before its parts wrap onto another row; never under <see cref="MinWidth"/>.</summary>
    /// <param name="width">The room the window has for it, in pixels.</param>
    internal void Fit(double width)
    {
        double parts = Math.Max(MinWidth, width) - Card.Padding.Left - Card.Padding.Right;
        if (Math.Abs(_parts.MaxWidth - parts) > 0.5)
        {
            _parts.MaxWidth = parts;
        }
    }

    /// <summary>Builds the bar (hidden), snapping as the hammer.</summary>
    public BuildingBar()
    {
        foreach (var mode in Enum.GetValues<PlaceTool.SnapModes>())
        {
            var (label, tip) = mode switch
            {
                PlaceTool.SnapModes.Manual => ("build.snap.manual", "build.snapManual"),
                PlaceTool.SnapModes.Free => ("build.snap.free", "build.snapFree"),
                _ => ("build.snap.auto", "build.snapAuto"),
            };
            var b = new Button { Content = Lang.T(label), FontSize = 11.5, Padding = new Thickness(8, 2), Tag = mode };
            b.Tip(tip);
            b.Click += (_, _) => SetSnapMode(mode);
            SnapModes.Children.Add(b);
        }
        GridBox.SelectionChanged += (_, _) => { _prefs.Set("build.grid", (double)GridStep); Changed?.Invoke(); };
        TurnBox.SelectionChanged += (_, _) => { _prefs.Set("build.turn", (double)TurnStep); Changed?.Invoke(); };
        LiftBox.SelectionChanged += (_, _) => { _prefs.Set("build.liftStep", (double)LiftStep); Changed?.Invoke(); };
        GridBox.Tip("build.grid");
        TurnBox.Tip("build.turn");
        LiftBox.Tip("build.liftStep");
        LiftReset.Tip("build.lift");
        BuilderBox.Tip("build.builder");
        LiftReset.Click += (_, _) => LiftResetAsked?.Invoke();
        static StackPanel Labelled(string label, params Control[] controls)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center });
            row.Children.AddRange(controls);
            return row;
        }
        static Border Sep() => new() { Width = 1, Height = 22, Background = Ui.Line, VerticalAlignment = VerticalAlignment.Center };
        _snapPart = Labelled(Lang.T("build.label.snapMode"), SnapModes, SnapPointText);
        _snapSep = Sep();
        _liftNow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { LiftText, LiftReset } };
        _builderPart = Labelled(Lang.T("build.label.builtBy"), BuilderBox);
        _builderSep = Sep();
        // The parts wrap onto a second row when the window is too narrow for one (the window sets the most it may take).
        Card = Ui.Card(_parts = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = 12,
            LineSpacing = 6,
            Children =
            {
                _snapPart,
                _snapSep,
                Labelled(Lang.T("build.label.grid"), GridBox),
                Labelled(Lang.T("build.label.turnBy"), TurnBox),
                Sep(),
                Labelled(Lang.T("build.label.lift"), LiftBox, _liftNow),
                _builderSep,
                _builderPart,
                Ui.HelpButton(() => HelpTopics.BuildingBar),
            },
        });
        Card.Padding = new Thickness(12, 6);
        Card.VerticalAlignment = VerticalAlignment.Top;
        Card.IsVisible = false;
        SetSnapMode(PlaceTool.SnapModes.Auto);
        ShowLift(0);
        Show(Uses.None, builder: false);
    }

    /// <summary>
    /// Takes the snapping, the grid and the steps from the window's settings, and keeps them there from now on.
    /// </summary>
    /// <param name="prefs">The window's settings.</param>
    internal void Remember(Prefs prefs)
    {
        _prefs = prefs;
        SetSnapMode(Enum.TryParse(prefs.Get("build.snapMode", ""), out PlaceTool.SnapModes mode) ? mode : PlaceTool.SnapModes.Auto);
        static void Choose(ComboBox box, float[] steps, double kept)
        {
            int i = Array.IndexOf(steps, (float)kept);
            if (i >= 0)
            {
                box.SelectedIndex = i;
            }
        }
        Choose(GridBox, Grids, prefs.Get("build.grid", (double)GridStep));
        Choose(TurnBox, Turns, prefs.Get("build.turn", (double)TurnStep));
        Choose(LiftBox, Lifts, prefs.Get("build.liftStep", (double)LiftStep));
    }

    /// <summary>Snaps pieces this way from now on (and the next time), its button lit.</summary>
    /// <param name="mode">Auto, Manual or Free.</param>
    public void SetSnapMode(PlaceTool.SnapModes mode)
    {
        SnapMode = mode;
        _prefs.Set("build.snapMode", mode.ToString());
        foreach (var b in SnapModes.Children.OfType<Button>())
        {
            b.Classes.Set("on", (PlaceTool.SnapModes)b.Tag! == mode);
        }
        Changed?.Invoke();
    }

    /// <summary>Shows the bar for a tool (its parts only), or hides it.</summary>
    /// <param name="use">The tool in use.</param>
    /// <param name="builder">Whether Built by is offered: in a world, not on the Workshop's plot (a blueprint keeps no builder).</param>
    public void Show(Uses use, bool builder)
    {
        Use = use;
        Card.IsVisible = use != Uses.None;
        _snapPart.IsVisible = _snapSep.IsVisible = use is Uses.Build or Uses.Select;
        // Select snaps as the hammer or not at all: a snap point is chosen by hand only while building.
        foreach (var b in SnapModes.Children.OfType<Button>())
        {
            b.IsVisible = use == Uses.Build || (PlaceTool.SnapModes)b.Tag! != PlaceTool.SnapModes.Manual;
        }
        if (use != Uses.Build)
        {
            SnapPointText.IsVisible = false;
        }
        _liftNow.IsVisible = use is Uses.Build or Uses.Paste;
        _builderPart.IsVisible = _builderSep.IsVisible = builder;
        ToolTip.SetTip(Card, use switch
        {
            Uses.Build => Lang.T("build.hint.hammer"),
            Uses.Select => Lang.T("build.hint.select"),
            Uses.Paste => Lang.T("build.hint.paste"),
            _ => null,
        });
    }

    /// <summary>Shows the lift the piece (Build) or the paste has now.</summary>
    /// <param name="metres">The lift, in metres (0: none).</param>
    public void ShowLift(float metres)
    {
        LiftText.Text = metres == 0 ? Lang.T("build.liftNone") : $"{metres:+0.0#;-0.0#} m";
        LiftReset.IsEnabled = metres != 0;
    }

    /// <summary>Snapping by hand while building: shows which snap point meets the others, or nothing (null).</summary>
    /// <param name="text">What to show, or null to hide it.</param>
    public void ShowSnapPoint(string? text)
    {
        SnapPointText.IsVisible = text != null;
        if (text != null)
        {
            SnapPointText.Text = text;
        }
    }
}
