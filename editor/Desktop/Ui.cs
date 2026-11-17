using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

/// <summary>
/// The look of the whole app, after Valheim's own menus: near-black panels with a bronze line round them, warm
/// off-white text in the game's serif (Averia Serif Libre, carried in the program), orange headings and accent,
/// squared buttons (plain, "primary" orange, "ghost" flat, "on" chosen), switches, and the same colours for every
/// box and list. Applied once to the application; panels use Card and the classes below.
/// </summary>
public static partial class Ui
{
    /// <summary>
    /// The palette's colours (background, panels, lines, text, accent...): the dark browns, bronze lines, warm white and
    /// orange of the game's menus.
    /// </summary>
    public static readonly Color BgColor = Color.Parse("#0f0c0a"), Panel2Color = Color.Parse("#241c15"), LineColor = Color.Parse("#4a3a2a"), Line2Color = Color.Parse("#6b543b"),
        TextColor = Color.Parse("#ece2cf"), MutedColor = Color.Parse("#a8997f"), AccentColor = Color.Parse("#f0a232"), AccentInkColor = Color.Parse("#1c1206"),
        WarnColor = Color.Parse("#e0553a"), OkColor = Color.Parse("#8cc26a"), HoverColor = Color.Parse("#30251b");

    /// <summary>The text's font, the game's own serif, carried in the program (SIL Open Font License, Assets/Fonts/OFL.txt).</summary>
    public static readonly FontFamily Font = new("avares://ValheimWorldEditor/Assets/Fonts#Averia Serif Libre");

    /// <summary>The corners of cards and of what is inside them (buttons, boxes, rows): nearly square, as the game's panels are.</summary>
    public static readonly CornerRadius CardRadius = new(4), ControlRadius = new(3);

    /// <summary>
    /// Brushes of the palette's colours; Panel is the cards' slightly see-through background, Live the green tag of
    /// changes already sent to the running game.
    /// </summary>
    public static readonly IBrush Bg = new SolidColorBrush(BgColor), Panel = new SolidColorBrush(Color.FromArgb(240, 20, 15, 11)), Panel2 = new SolidColorBrush(Panel2Color),
        Line = new SolidColorBrush(LineColor), Line2 = new SolidColorBrush(Line2Color), Text = new SolidColorBrush(TextColor), Muted = new SolidColorBrush(MutedColor),
        Accent = new SolidColorBrush(AccentColor), AccentInk = new SolidColorBrush(AccentInkColor), Warn = new SolidColorBrush(WarnColor), Ok = new SolidColorBrush(OkColor),
        Hover = new SolidColorBrush(HoverColor), Live = new SolidColorBrush(Color.Parse("#8ff0b4"));

    /// <summary>The solid background of side bars and dialogs (the cards' colour, not see-through).</summary>
    public static readonly IBrush Side = new SolidColorBrush(Color.FromRgb(22, 17, 13));

    /// <summary>The space inside every card, the same for all of them (whatever their size).</summary>
    public static readonly Thickness Pad = new(10);
    /// <summary>The space between cards.</summary>
    public const double Gap = 10;

    /// <summary>A floating card (tool options, the View panel...).</summary>
    public static Border Card(Control child) => AsCard(new Border { Child = child });

    /// <summary>
    /// Gives a border the floating cards' look (background, line, rounded corners, padding, shadow); its own place,
    /// size and child stay.
    /// </summary>
    /// <returns>The same border.</returns>
    public static Border AsCard(Border b)
    {
        b.Background = Panel;
        b.BorderBrush = Line;
        b.BorderThickness = new Thickness(1);
        b.CornerRadius = CardRadius;
        b.Padding = Pad;
        // A dark line inside the bronze one, as the game's panels have, and the shadow under the card.
        b.BoxShadow = BoxShadows.Parse("inset 0 0 0 1 #b3000000, 0 6 24 0 #73000000");
        return b;
    }

    /// <summary>A small heading in capitals (VIEW, OVERLAYS...).</summary>
    /// <param name="text">The heading, turned to capitals.</param>
    /// <param name="top">The space above it.</param>
    public static TextBlock Heading(string text, double top = 10) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 11,
        FontWeight = FontWeight.Bold,
        Foreground = Muted,
        LetterSpacing = 1,
        Margin = new Thickness(0, top, 0, 3),
    };

    /// <summary>A name with a count after it in grey, right-aligned (the View panel's switches).</summary>
    public static Control Counted(string name, int count)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        g.Children.Add(new TextBlock { Text = name });
        var n = new TextBlock { Text = count.ToString("N0"), Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(n, 1);
        g.Children.Add(n);
        return g;
    }

    /// <summary>Puts a control in a grid column; returns it, for building rows inline.</summary>
    /// <param name="c">The control.</param>
    /// <param name="col">The column.</param>
    public static Control Col(Control c, int col)
    {
        Grid.SetColumn(c, col);
        return c;
    }

    /// <summary>A tool card's labelled row: the label, the input, and optionally something after it (a value, a unit, a button).</summary>
    /// <param name="label">The label, also the input's name for screen readers and tips.</param>
    /// <param name="input">The input.</param>
    /// <param name="after">What goes after it (null: nothing).</param>
    /// <param name="columns">The three columns' widths.</param>
    public static Grid Row(string label, Control input, Control? after = null, string columns = "70,*,46")
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions(columns) };
        var l = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        Tips.Label(l, input);
        g.Children.Add(l);
        Grid.SetColumn(input, 1);
        g.Children.Add(input);
        if (after != null)
        {
            Grid.SetColumn(after, 2);
            g.Children.Add(after);
        }
        return g;
    }

    /// <summary>
    /// A slider snapping to <paramref name="step"/> whose value is shown through <paramref name="format"/> in
    /// <paramref name="shown"/> (set up as a row's value) and passed to <paramref name="set"/> on each change.
    /// </summary>
    public static Slider Slide(double min, double max, double step, double value, TextBlock shown, Func<double, string> format, Action<double> set)
    {
        var s = new Slider { Minimum = min, Maximum = max, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true, Value = value };
        shown.Text = format(value);
        StyleValueText(shown);
        s.ValueChanged += (_, e) => { shown.Text = format(e.NewValue); set(e.NewValue); };
        return s;
    }

    /// <summary>Styles a row's value text: small, centred vertically, a little apart from the slider before it.</summary>
    /// <param name="shown">The value's text block.</param>
    public static void StyleValueText(TextBlock shown)
    {
        shown.FontSize = 12;
        shown.VerticalAlignment = VerticalAlignment.Center;
        shown.Margin = new Thickness(6, 0, 0, 0);
    }

    /// <summary>Adds style classes ("primary", "ghost", "on", "rail", "switch"...) to an element and returns it.</summary>
    public static T Classed<T>(this T c, params string[] classes) where T : StyledElement
    {
        foreach (string k in classes)
        {
            c.Classes.Add(k);
        }
        return c;
    }
}
