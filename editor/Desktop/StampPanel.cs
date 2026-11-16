using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Landform panel's Picture kind: a greyscale picture (a built-in one, or one loaded from a file) pressed whole
/// into the ground by a click. White parts go the whole height, black parts stay; a negative height presses it down.
/// The pictures are the brushes' stamps (<c>Stamps</c>), so one loaded here is a brush shape too.
/// </summary>
public sealed class StampPanel
{
    /// <summary>The kind's rows in the Landform panel: the picture, radius, height and turn, then loading and forgetting one.</summary>
    public Control Rows { get; }
    /// <summary>Raised when the picture, radius, height or turn changes, so the preview can follow.</summary>
    public event Action? Changed;
    /// <summary>Load picture…: the window picks a file and adds it to the stamps.</summary>
    public event Action? LoadAsked;
    /// <summary>Forget picture: the window drops this loaded picture from the stamps.</summary>
    public event Action<Stamps.Stamp>? ForgetAsked;

    /// <summary>The pictures offered, by their names in the language in use.</summary>
    internal ComboBox PresetBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 400 };
    /// <summary>Half the picture's side in metres (2..80).</summary>
    internal Slider RadiusSlider { get; }
    /// <summary>How far the white parts go, in metres; negative presses them down.</summary>
    internal NumericUpDown HeightBox { get; } = new() { Value = 4, Increment = 0.5m, FormatString = "0.0#", FontSize = 12 };
    /// <summary>The picture's turn in degrees (-180..180).</summary>
    internal Slider TurnSlider { get; }
    /// <summary>Load picture…: asks the window for a picture file (<c>LoadAsked</c>).</summary>
    internal Button LoadButton { get; } = new() { Content = Lang.T("landform.label.loadPicture"), FontSize = 12 };
    /// <summary>Forget picture: drops the loaded picture chosen (built-in ones stay); shown only for a loaded one.</summary>
    internal Button ForgetButton { get; } = new() { Content = Lang.T("landform.label.forgetPicture"), FontSize = 12 };
    /// <summary>The pictures in the list, in its order.</summary>
    private List<Stamps.Stamp> _offered = [];

    /// <summary>The picture chosen; null when the list is empty.</summary>
    public Stamps.Stamp? Stamp => PresetBox.SelectedIndex >= 0 && PresetBox.SelectedIndex < _offered.Count ? _offered[PresetBox.SelectedIndex] : null;
    /// <summary>How far the picture's white parts go, in metres (negative: down).</summary>
    public float Height => (float)(HeightBox.Value ?? 0);
    /// <summary>The edit's name for the history, from the picture chosen.</summary>
    public string Label => Lang.T("landform.undo", Stamp?.Shown ?? "");

    /// <summary>
    /// The picture as a brush of its own (the stamp engine works with one): the picture as its shape, with the radius
    /// and turn set here. Not the sculpt brushes' brush, which keeps its own shape and size.
    /// </summary>
    public Brush Brush => new() { Shape = BrushShape.Stamp, StampData = Stamp?.Data, StampLabel = Stamp?.Shown ?? "", Radius = (float)RadiusSlider.Value, Turn = (float)TurnSlider.Value };

    /// <summary>Builds the rows with the built-in pictures, the first chosen.</summary>
    public StampPanel()
    {
        TextBlock radiusV = new(), turnV = new();
        RadiusSlider = Ui.Slide(2, 80, 1, 16, radiusV, v => $"{v:0} m", _ => Changed?.Invoke());
        TurnSlider = Ui.Slide(-180, 180, 1, 0, turnV, v => $"{v:0}°", _ => Changed?.Invoke());
        HeightBox.ValueChanged += (_, _) => Changed?.Invoke();
        PresetBox.SelectionChanged += (_, _) =>
        {
            ForgetButton.IsVisible = Stamp is { Loaded: true };
            Changed?.Invoke();
        };
        LoadButton.Click += (_, _) => LoadAsked?.Invoke();
        ForgetButton.Click += (_, _) =>
        {
            if (Stamp is { Loaded: true } stamp)
            {
                ForgetAsked?.Invoke(stamp);
            }
        };
        PresetBox.Tip("landform.picture");
        RadiusSlider.Tip("landform.pictureRadius");
        HeightBox.Tip("landform.pictureHeight");
        TurnSlider.Tip("landform.pictureTurn");
        LoadButton.Tip("landform.loadPicture");
        ForgetButton.Tip("landform.forgetPicture");
        // The rows every kind has (preset, radius, height, turn) first and under the same names, then this kind's own.
        Rows = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ui.Row(Lang.T("common.preset"), PresetBox),
                Ui.Row(Lang.T("shape.label.radius"), RadiusSlider, radiusV),
                Ui.Row(Lang.T("common.height"), HeightBox, new TextBlock { Text = Lang.T("common.metres"), FontSize = 12, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }),
                Ui.Row(Lang.T("brush.label.turn"), TurnSlider, turnV),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { LoadButton, ForgetButton } },
            },
        };
        Fill(Stamps.BuiltIn);
    }

    /// <summary>
    /// The stamps changed (one loaded or forgotten): the list is filled again with those that are landforms
    /// (<c>Stamps.Landforms</c>), keeping the picture chosen when it is still there, else the first.
    /// </summary>
    /// <param name="stamps">Every stamp, built-in and loaded.</param>
    public void Fill(IEnumerable<Stamps.Stamp> stamps)
    {
        var chosen = Stamp;
        _offered = Stamps.Landforms(stamps);
        PresetBox.ItemsSource = _offered.Select(s => s.Shown).ToList();
        PresetBox.SelectedIndex = Math.Max(0, chosen == null ? 0 : _offered.IndexOf(chosen));
        // (Said here too: the list filled again with the same entry chosen does not say the selection changed.)
        ForgetButton.IsVisible = Stamp is { Loaded: true };
    }

    /// <summary>Chooses a picture of the list (one just loaded).</summary>
    /// <param name="stamp">The picture.</param>
    public void Choose(Stamps.Stamp stamp)
    {
        int i = _offered.IndexOf(stamp);
        if (i >= 0)
        {
            PresetBox.SelectedIndex = i;
        }
    }

    /// <summary>
    /// The picture the controls describe, for the preview: the metres it adds at (x, z) metres east and north of the
    /// click; null when no picture is chosen.
    /// </summary>
    /// <returns>How far it reaches, its square outline and the shape.</returns>
    internal LandformGhost Ghost()
    {
        var brush = Brush;
        float height = Height;
        return new LandformGhost(brush.Reach, brush.StampData == null ? null : (x, z) => height * brush.Weight(x, z), Square: brush.Outline, Least: 0.05f);
    }
}
