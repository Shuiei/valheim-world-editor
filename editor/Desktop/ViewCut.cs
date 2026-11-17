using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The view's cut, one control for every 3D view (the status bar's slider): nothing higher than so many metres above
/// a level is drawn, and the cursor goes through what is hidden, to see and build inside. The level is the ground in
/// the middle of the view in a world (the cut follows the view as it moves), the plot in the Workshop, and the floor
/// of its entrance in a dungeon. The slider's right end is off, as it starts. A world and a dungeon each keep their
/// own cut while the other is looked at; the Workshop starts with none.
/// </summary>
public sealed class ViewCut
{
    /// <summary>What the cut's metres are counted from.</summary>
    public enum Level
    {
        /// <summary>A world: the ground (or the sea) in the middle of the view, which the cut follows.</summary>
        Ground,
        /// <summary>The Workshop: its plot.</summary>
        Plot,
        /// <summary>A dungeon: about the floor of its entrance.</summary>
        Dungeon,
    }

    /// <summary>The slider's right end (off) in a world and the Workshop: buildings up to 39 m are cut.</summary>
    internal const double End = 40;
    /// <summary>The slider's right end (off) in a dungeon, whose rooms are lower: up to 29 m.</summary>
    internal const double DungeonEnd = 30;

    /// <summary>The cut's height above its level, in metres (1 m steps, a half wall's height); the right end is off.</summary>
    /// <remarks>
    /// The theme's slider is 52 pixels tall around its thin track: the margin takes that room back, so the status bar
    /// stays one line of text tall.
    /// </remarks>
    internal Slider Slider { get; } = new Slider { Minimum = 0, Maximum = End, Value = End, SmallChange = 1, LargeChange = 2, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 110, VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(0, -18) }.Tip("view.cut");
    /// <summary>The cut's height, or that it is off.</summary>
    internal TextBlock Text { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, MinWidth = 70 };
    /// <summary>The cut's text and slider, for the status bar.</summary>
    public StackPanel Box { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>What the cut's metres are counted from now.</summary>
    public Level From { get; private set; } = Level.Ground;
    /// <summary>Whether something is cut away (the slider is not at its right end).</summary>
    public bool On => Slider.Value < Slider.Maximum;
    /// <summary>The cut changed (its height, its level, on or off): what points into the view looks again.</summary>
    public event Action? Changed;

    /// <summary>The view cut.</summary>
    private readonly GlView _view;
    /// <summary>The Place tool, which builds under the cut: its pieces above it are not pointed at.</summary>
    private readonly PlaceTool _place;
    /// <summary>The height of the plot or of the dungeon's floor (<see cref="Level.Plot"/>, <see cref="Level.Dungeon"/>).</summary>
    private float _floor;
    /// <summary>Each level's cut while another is in use, in metres; null: off.</summary>
    private readonly double?[] _kept = new double?[3];
    /// <summary>True while the slider is set for another level, so it is applied once, at the end.</summary>
    private bool _switching;

    /// <summary>Makes the view's cut, off, for a world.</summary>
    /// <param name="view">The 3D view.</param>
    /// <param name="place">The Place tool.</param>
    public ViewCut(GlView view, PlaceTool place)
    {
        _view = view;
        _place = place;
        Box.Children.Add(Text);
        Box.Children.Add(Slider);
        Tips.Label(Text, Slider);
        Slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && !_switching)
            {
                Apply();
            }
        };
        Apply();
    }

    /// <summary>Cuts so many metres above the level (at or past the slider's right end: off).</summary>
    /// <param name="metres">The height above the level, in metres.</param>
    public void Set(double metres) => Slider.Value = Math.Clamp(metres, 0, Slider.Maximum);

    /// <summary>Nothing cut away.</summary>
    public void Off() => Slider.Value = Slider.Maximum;

    /// <summary>
    /// A world's cut: counted from the ground in the middle of the view. Coming from a dungeon, the world's cut from
    /// before it is back.
    /// </summary>
    /// <param name="forget">The world's cut starts off, whatever it was (out of the Workshop).</param>
    public void FromGround(bool forget = false)
    {
        if (forget)
        {
            _kept[(int)Level.Ground] = null;
        }
        bool already = From == Level.Ground;
        Switch(Level.Ground, 0);
        if (forget && already)
        {
            Off();
        }
    }

    /// <summary>The Workshop's cut: counted from the plot, off at first (each time a building is opened on it).</summary>
    /// <param name="ground">The plot's height.</param>
    public void FromPlot(float ground)
    {
        _kept[(int)Level.Plot] = null;
        bool already = From == Level.Plot;
        Switch(Level.Plot, ground);
        if (already)
        {
            Off();
        }
    }

    /// <summary>
    /// A dungeon's cut: counted from its floor. Coming from the world, the cut is the one the dungeon had last; in a
    /// dungeon already, it keeps its metres, from this floor.
    /// </summary>
    /// <param name="floor">The floor's height.</param>
    public void FromDungeon(float floor) => Switch(Level.Dungeon, floor);

    /// <summary>
    /// Counts the cut from another level: the one left keeps its cut for when it is back, the slider takes the new
    /// level's range and the cut it had.
    /// </summary>
    /// <param name="to">The level to count from.</param>
    /// <param name="floor">Its height, when it has one of its own (the plot, a dungeon's floor).</param>
    private void Switch(Level to, float floor)
    {
        _floor = floor;
        if (to != From)
        {
            _kept[(int)From] = On ? Slider.Value : null;
            From = to;
            double end = to == Level.Dungeon ? DungeonEnd : End;
            _switching = true;
            // The value first when the range shrinks (it would be held to the old end), last when it grows.
            Slider.Value = Math.Min(_kept[(int)to] ?? end, end);
            Slider.Maximum = end;
            Slider.Value = _kept[(int)to] ?? end;
            _switching = false;
        }
        Apply();
    }

    /// <summary>The view and the Place tool cut as the slider and the level say, and the text says where.</summary>
    private void Apply()
    {
        double metres = Slider.Value;
        bool on = On;
        if (From == Level.Ground)
        {
            _view.CutY = null;
            _view.CutAbove = on ? (float)metres : null;
        }
        else
        {
            _view.CutAbove = null;
            _view.CutY = on ? _floor + (float)metres : null;
        }
        _place.CutY = _view.CutY;
        Text.Text = on ? Lang.T("cut.at", metres) : Lang.T("cut.off");
        Changed?.Invoke();
    }
}
