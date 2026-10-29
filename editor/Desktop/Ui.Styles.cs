using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace TerrainEditor.Desktop;

public static partial class Ui
{
    /// <summary>
    /// Applies the look to the application, once at start: Fluent's colour resources set to the palette, and the
    /// styles for windows, buttons and their classes, lists, number boxes and switches.
    /// </summary>
    public static void Apply(Application app)
    {
        var r = app.Resources;
        // Fluent's own colours, the web editor's.
        r["SystemAccentColor"] = AccentColor;
        r["SystemAccentColorLight1"] = Color.Parse("#eab565");
        r["SystemAccentColorLight2"] = Color.Parse("#f0c47f");
        r["SystemAccentColorLight3"] = Color.Parse("#f5d5a0");
        r["SystemAccentColorDark1"] = Color.Parse("#c98f37");
        r["SystemAccentColorDark2"] = Color.Parse("#a87428");
        r["SystemAccentColorDark3"] = Color.Parse("#7a5a2a");
        r["ControlCornerRadius"] = new CornerRadius(6);
        r["OverlayCornerRadius"] = new CornerRadius(8);
        // Buttons.
        r["ButtonBackground"] = Panel2;
        r["ButtonBackgroundPointerOver"] = Hover;
        r["ButtonBackgroundPressed"] = new SolidColorBrush(Color.Parse("#2a313c"));
        r["ButtonBackgroundDisabled"] = Panel2;
        r["ButtonForeground"] = Text;
        r["ButtonForegroundPointerOver"] = Text;
        r["ButtonForegroundPressed"] = Text;
        r["ButtonForegroundDisabled"] = new SolidColorBrush(Color.FromArgb(110, 230, 233, 238));
        r["ButtonBorderBrush"] = Line;
        r["ButtonBorderBrushPointerOver"] = Line2;
        r["ButtonBorderBrushPressed"] = Line2;
        r["ButtonBorderBrushDisabled"] = Line;
        // Toggle buttons (chips: biomes, Thermal / Water...): chosen = amber outline, like the web's .on.
        r["ToggleButtonBackground"] = Panel2;
        r["ToggleButtonBackgroundPointerOver"] = Hover;
        r["ToggleButtonBackgroundPressed"] = Hover;
        r["ToggleButtonBorderBrush"] = Line;
        r["ToggleButtonBorderBrushPointerOver"] = Line2;
        r["ToggleButtonForeground"] = Text;
        r["ToggleButtonForegroundPointerOver"] = Text;
        var chosenBg = new SolidColorBrush(Color.Parse("#2c2416"));
        foreach (string? state in new[] { "", "PointerOver", "Pressed" })
        {
            r[$"ToggleButtonBackgroundChecked{state}"] = chosenBg;
            r[$"ToggleButtonForegroundChecked{state}"] = Accent;
            r[$"ToggleButtonBorderBrushChecked{state}"] = Accent;
        }
        // Text boxes, number boxes and lists.
        foreach (string? state in new[] { "", "PointerOver", "Focused" })
        {
            r[$"TextControlBackground{state}"] = Panel2;
            r[$"TextControlForeground{state}"] = Text;
        }
        r["TextControlBorderBrush"] = Line;
        r["TextControlBorderBrushPointerOver"] = Line2;
        r["TextControlBorderBrushFocused"] = Accent;
        r["TextControlPlaceholderForeground"] = Muted;
        r["TextControlPlaceholderForegroundPointerOver"] = Muted;
        r["TextControlPlaceholderForegroundFocused"] = Muted;
        r["ComboBoxBackground"] = Panel2;
        r["ComboBoxBackgroundPointerOver"] = Hover;
        r["ComboBoxBackgroundPressed"] = Hover;
        r["ComboBoxBackgroundFocused"] = Panel2;
        r["ComboBoxBorderBrush"] = Line;
        r["ComboBoxBorderBrushPointerOver"] = Line2;
        r["ComboBoxBorderBrushPressed"] = Accent;
        r["ComboBoxDropDownBackground"] = Panel2;
        r["ComboBoxDropDownBorderBrush"] = Line2;
        r["ComboBoxItemBackgroundSelected"] = new SolidColorBrush(Color.Parse("#3a2f1c"));
        r["ComboBoxItemBackgroundSelectedPointerOver"] = new SolidColorBrush(Color.Parse("#4a3b22"));
        r["ComboBoxItemBackgroundPointerOver"] = Hover;
        r["ListBoxItemBackgroundSelected"] = new SolidColorBrush(Color.Parse("#3a2f1c"));
        r["ListBoxItemBackgroundSelectedPointerOver"] = new SolidColorBrush(Color.Parse("#4a3b22"));
        r["ListBoxItemBackgroundPointerOver"] = Hover;
        // Scroll bars: thin, a quiet thumb on no track.
        r["ScrollBarSize"] = 10.0;
        r["ScrollBarTrackFill"] = Brushes.Transparent;
        r["ScrollBarTrackFillPointerOver"] = Brushes.Transparent;
        r["ScrollBarTrackStroke"] = Brushes.Transparent;
        r["ScrollBarTrackStrokePointerOver"] = Brushes.Transparent;
        r["ScrollBarThumbFill"] = Line2;
        r["ScrollBarThumbFillPointerOver"] = new SolidColorBrush(Color.Parse("#566173"));
        r["ScrollBarThumbFillPressed"] = Muted;
        r["ScrollBarButtonArrowForeground"] = Brushes.Transparent;
        r["ScrollBarButtonArrowForegroundPointerOver"] = Muted;
        r["ScrollBarButtonBackgroundPointerOver"] = Brushes.Transparent;
        r["ScrollBarBackground"] = Brushes.Transparent;
        r["ScrollBarBackgroundPointerOver"] = Brushes.Transparent;
        r["ScrollBarThumbBackgroundColor"] = Line2Color;
        r["CheckBoxMinHeight"] = 26.0;
        // Chosen list rows: a quiet amber, not the full accent.
        r["SystemControlHighlightListAccentLowBrush"] = new SolidColorBrush(Color.Parse("#3a2f1c"));
        r["SystemControlHighlightListAccentMediumBrush"] = new SolidColorBrush(Color.Parse("#4a3b22"));
        r["SystemControlHighlightListAccentHighBrush"] = new SolidColorBrush(Color.Parse("#574427"));
        r["SystemControlHighlightListLowBrush"] = Hover;
        r["SystemControlHighlightListMediumBrush"] = new SolidColorBrush(Color.Parse("#2a313c"));
        r["ToolTipBackground"] = Panel2;
        r["ToolTipBorderBrush"] = Line2;
        r["ToolTipForeground"] = Text;
        r["ExpanderHeaderBackground"] = Brushes.Transparent;
        r["ExpanderHeaderBackgroundPointerOver"] = Hover;
        r["ExpanderHeaderBackgroundPressed"] = Hover;
        r["ExpanderHeaderBorderBrush"] = Brushes.Transparent;
        r["ExpanderHeaderBorderBrushPointerOver"] = Line;
        r["ExpanderContentBackground"] = Brushes.Transparent;
        r["ExpanderContentBorderBrush"] = Line;
        // Folds inside cards: no padding or border of their own (the card's padding is the only one).
        r["ExpanderHeaderPadding"] = new Thickness(0);
        r["ExpanderContentPadding"] = new Thickness(0, 6, 0, 0);
        r["ExpanderHeaderBorderThickness"] = new Thickness(0);
        r["ExpanderContentDownBorderThickness"] = new Thickness(0);
        r["ExpanderContentUpBorderThickness"] = new Thickness(0);
        r["ExpanderContentLeftBorderThickness"] = new Thickness(0);
        r["ExpanderContentRightBorderThickness"] = new Thickness(0);
        r["ExpanderMinHeight"] = 32.0;

        var s = app.Styles;
        s.Add(new Style(x => x.OfType<Window>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, Bg), new Setter(TemplatedControl.FontSizeProperty, 13.0) } });
        s.Add(new Style(x => x.OfType<Button>()) { Setters = { new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(7)), new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 5)) } });
        // Scroll bars beside the content when they show (not drawn over it), and only then.
        s.Add(new Style(x => x.OfType<ScrollViewer>()) { Setters = { new Setter(ScrollViewer.AllowAutoHideProperty, false), new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto) } });
        // List rows as compact as the web editor's.
        s.Add(new Style(x => x.OfType<ListBoxItem>()) { Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(8, 4)), new Setter(Layoutable.MinHeightProperty, 0.0) } });
        // Check boxes as compact as the web editor's.
        s.Add(new Style(x => x.OfType<CheckBox>()) { Setters = { new Setter(Layoutable.MinHeightProperty, 26.0) } });
        s.Add(new Style(x => x.OfType<ToggleButton>()) { Setters = { new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(7)) } });
        // Number boxes without the theme's big up/down buttons (they squeezed the numbers out of narrow
        // panels); the arrow keys and the mouse wheel still step them.
        s.Add(new Style(x => x.OfType<NumericUpDown>())
        {
            Setters = { new Setter(NumericUpDown.ShowButtonSpinnerProperty, false), new Setter(Layoutable.MinWidthProperty, 64.0) },
        });

        // A style for the button's inner presenter (what the theme's hover and press states change).
        Style Presenter(Func<Selector?, Selector> button, params Setter[] setters)
        {
            var st = new Style(x => button(x).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
            foreach (var set in setters)
            {
                st.Setters.Add(set);
            }
            return st;
        }
        Setter Bgs(IBrush b) => new(ContentPresenter.BackgroundProperty, b);
        Setter Border(IBrush b) => new(ContentPresenter.BorderBrushProperty, b);
        Setter Fore(IBrush b) => new(ContentPresenter.ForegroundProperty, b);

        // Primary: the amber button (Save).
        var hoverAmber = new SolidColorBrush(Color.Parse("#eab565"));
        s.Add(new Style(x => x.OfType<Button>().Class("primary")) { Setters = { new Setter(TemplatedControl.FontWeightProperty, FontWeight.SemiBold) } });
        s.Add(Presenter(x => x.OfType<Button>().Class("primary"), Bgs(Accent), Border(Accent), Fore(AccentInk)));
        s.Add(Presenter(x => x.OfType<Button>().Class("primary").Class(":pointerover"), Bgs(hoverAmber), Border(hoverAmber), Fore(AccentInk)));
        s.Add(Presenter(x => x.OfType<Button>().Class("primary").Class(":pressed"), Bgs(Accent), Border(Accent), Fore(AccentInk)));
        s.Add(Presenter(x => x.OfType<Button>().Class("primary").Class(":disabled"), Bgs(Accent), Border(Accent), Fore(AccentInk)));
        s.Add(new Style(x => x.OfType<Button>().Class("primary").Class(":disabled")) { Setters = { new Setter(Visual.OpacityProperty, 0.4) } });
        // Ghost: flat until the pointer is over it (top bar buttons).
        s.Add(Presenter(x => x.OfType<Button>().Class("ghost"), Bgs(Brushes.Transparent), Border(Brushes.Transparent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("ghost").Class(":pointerover"), Bgs(Panel2), Border(Line)));
        s.Add(Presenter(x => x.OfType<Button>().Class("ghost").Class(":disabled"), Bgs(Brushes.Transparent), Border(Brushes.Transparent)));
        // On: the chosen one of a set (amber outline and text).
        s.Add(Presenter(x => x.OfType<Button>().Class("on"), Border(Accent), Fore(Accent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("on").Class(":pointerover"), Border(Accent), Fore(Accent)));
        // The tool rail: flat buttons, the chosen tool filled amber.
        s.Add(Presenter(x => x.OfType<Button>().Class("rail"), Bgs(Brushes.Transparent), Border(Brushes.Transparent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("rail").Class(":pointerover"), Bgs(Panel2), Border(Brushes.Transparent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("rail").Class("on"), Bgs(Accent), Border(Accent), Fore(AccentInk)));
        s.Add(Presenter(x => x.OfType<Button>().Class("rail").Class("on").Class(":pointerover"), Bgs(hoverAmber), Border(hoverAmber), Fore(AccentInk)));
        s.Add(new Style(x => x.OfType<Button>().Class("rail").Class("on")) { Setters = { new Setter(TemplatedControl.FontWeightProperty, FontWeight.SemiBold) } });

        // Switches (the web's View panel toggles): a CheckBox drawn as a small track and knob.
        s.Add(new Style(x => x.OfType<CheckBox>().Class("switch")) { Setters = { new Setter(TemplatedControl.TemplateProperty, SwitchTemplate()) } });
    }

    /// <summary>
    /// The "switch" class's template: a check box drawn as a small track and knob (amber, knob right, when on),
    /// with its content to the right.
    /// </summary>
    private static FuncControlTemplate<CheckBox> SwitchTemplate() => new((cb, scope) =>
    {
        var knob = new Border { Width = 13, Height = 13, CornerRadius = new CornerRadius(7), Margin = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        var track = new Border { Width = 30, Height = 17, CornerRadius = new CornerRadius(9), Child = knob, VerticalAlignment = VerticalAlignment.Center };
        var content = new ContentPresenter { Name = "PART_ContentPresenter", VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(10, 0, 0, 0) };
        content.Bind(ContentPresenter.ContentProperty, cb.GetObservable(ContentControl.ContentProperty));
        void Sync()
        {
            bool on = cb.IsChecked == true;
            track.Background = on ? Accent : new SolidColorBrush(Color.Parse("#3a414c"));
            knob.Background = on ? Brushes.White : new SolidColorBrush(Color.Parse("#cfd5de"));
            knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        cb.PropertyChanged += (_, e) => { if (e.Property == ToggleButton.IsCheckedProperty) { Sync(); } };
        Sync();
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Background = Brushes.Transparent, Margin = new Thickness(0, 3) };
        Grid.SetColumn(content, 1);
        root.Children.Add(track);
        root.Children.Add(content);
        content.RegisterInNameScope(scope);
        return root;
    });
}
