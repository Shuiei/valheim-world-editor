using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>What a landform is made from: each kind has its own presets and its own engine.</summary>
public enum LandformKind
{
    /// <summary>A formula gives the metres to add at each point (<c>ShapePanel</c>, <c>EditSession.Shape</c>).</summary>
    Formula,
    /// <summary>A mountain made from a seed, past the game's limit (<c>MountainPanel</c>, <c>Mountain</c>).</summary>
    Mountain,
    /// <summary>A greyscale picture pressed into the ground (<c>StampPanel</c>, <c>Sculpt.StampOnce</c>).</summary>
    Picture,
}

/// <summary>
/// The Landform tool's panel: a click puts a whole landform into the ground. Kind says what it is made from (a
/// formula, the mountain generator or a picture); under it come that kind's presets and settings, the ones every kind
/// has (preset, radius, height, turn) in the same place and under the same names, then whether it obeys the game's
/// ground limit, and how to place it.
/// </summary>
public sealed class LandformPanel
{
    /// <summary>The panel's card, shown beside the tool rail while the Landform tool is picked.</summary>
    public Control Card { get; }
    /// <summary>The Formula kind's rows and values.</summary>
    public ShapePanel Formula { get; } = new();
    /// <summary>The Mountain kind's rows and values.</summary>
    public MountainPanel Mountain { get; } = new();
    /// <summary>The Picture kind's rows and values.</summary>
    public StampPanel Picture { get; } = new();
    /// <summary>Raised when the kind or any of the kind in use's settings changes, so the preview can follow.</summary>
    public event Action? Changed;

    /// <summary>The kinds, in <see cref="LandformKind"/>'s order.</summary>
    internal ComboBox KindBox { get; }
    /// <summary>What the kind chosen makes, in a line.</summary>
    internal TextBlock HelpText { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>Whether the kind chosen obeys the game's ground limit, as No limit stands.</summary>
    internal TextBlock LimitText { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    /// <summary>Whether No limit is on (the window says so: its switch is with the brushes).</summary>
    private bool _noLimit;

    /// <summary>The kind chosen (Formula when none is).</summary>
    public LandformKind Kind => (LandformKind)Math.Max(0, KindBox.SelectedIndex);

    /// <summary>Builds the card with the Formula kind chosen.</summary>
    public LandformPanel()
    {
        KindBox = new ComboBox
        {
            ItemsSource = new[] { Lang.T("landform.kind.formula"), Lang.T("landform.kind.mountain"), Lang.T("landform.kind.picture") },
            SelectedIndex = 0,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        KindBox.Tip("landform.kind");
        KindBox.SelectionChanged += (_, _) =>
        {
            Sync();
            Changed?.Invoke();
        };
        // Only the kind in use says it changed: the others' values are not what is shown.
        Formula.Changed += () => Raise(LandformKind.Formula);
        Mountain.Changed += () => Raise(LandformKind.Mountain);
        Picture.Changed += () => Raise(LandformKind.Picture);
        Card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 300,
                Spacing = 6,
                Children =
                {
                    Ui.Title(Lang.T("rail.label.landform"), HelpTopics.Tool(ToolMode.Landform)),
                    Ui.Row(Lang.T("landform.label.kind"), KindBox),
                    HelpText,
                    Formula.Rows,
                    Mountain.Rows,
                    Picture.Rows,
                    LimitText,
                    new TextBlock
                    {
                        Text = Lang.T("landform.hint.click"),
                        FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        });
        Sync();
    }

    /// <summary>Raises <c>Changed</c> for a change in one kind's settings, when that kind is the one in use.</summary>
    /// <param name="kind">The kind whose settings changed.</param>
    private void Raise(LandformKind kind)
    {
        if (Kind == kind)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Shows the rows, the help line and the limit line of the kind chosen.</summary>
    private void Sync()
    {
        var kind = Kind;
        Formula.Rows.IsVisible = kind == LandformKind.Formula;
        Mountain.Rows.IsVisible = kind == LandformKind.Mountain;
        Picture.Rows.IsVisible = kind == LandformKind.Picture;
        HelpText.Text = kind switch
        {
            LandformKind.Formula => Lang.T("landform.help.formula"),
            LandformKind.Mountain => Lang.T("landform.help.mountain"),
            _ => Lang.T("landform.help.picture"),
        };
        // A mountain always goes past the limit; the other kinds obey it unless No limit is on.
        bool past = kind == LandformKind.Mountain || _noLimit;
        LimitText.Text = kind == LandformKind.Mountain ? Lang.T("landform.limit.never") : _noLimit ? Lang.T("landform.limit.off") : Lang.T("landform.limit.kept");
        LimitText.Foreground = past ? Ui.Accent : Ui.Muted;
    }

    /// <summary>No limit was switched (its switch is with the brushes): the limit line says how the kind stands now.</summary>
    /// <param name="on">Whether the ground tools go past the game's ±8 m.</param>
    public void ShowNoLimit(bool on)
    {
        _noLimit = on;
        Sync();
    }

    /// <summary>What the 3D view shows under the pointer for the kind chosen: its outline and its preview.</summary>
    /// <param name="noise">The noise a formula's n(x, z) reads (the brushes').</param>
    internal LandformGhost Ghost(Func<Noise> noise) => Kind switch
    {
        LandformKind.Formula => new LandformGhost(Formula.Radius, Formula.Shape(noise), Least: 0.05f),
        LandformKind.Mountain => new LandformGhost(Mountain.Spec.Reach, Desktop.Mountain.Shape(Mountain.Spec), MustFit: true),
        _ => Picture.Ghost(),
    };
}
