using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Building: who new pieces are built by (the player ids: the list's entries).
    /// </summary>
    internal ComboBox BuilderBox { get; } = new() { FontSize = 12, MinWidth = 200, MaxWidth = 260 };

    /// <summary>
    /// The player id of each Built by entry, in order: the players, then -1 (another id, asked for) and 0 (nobody).
    /// </summary>
    internal List<long> BuilderIds { get; } = [];

    /// <summary>
    /// True while the Built by list is filled, so it is not taken for a choice.
    /// </summary>
    private bool _fillingBuilders;

    /// <summary>
    /// Draw height lines.
    /// </summary>
    internal CheckBox ContourBox { get; } = new() { Content = Lang.T("view.label.contour"), FontSize = 12 };

    /// <summary>
    /// How far apart the height lines are, in metres.
    /// </summary>
    internal ComboBox ContourStepBox { get; } = new() { ItemsSource = new[] { "1", "2", "5", "10" }, SelectedIndex = 1, FontSize = 12, MinWidth = 60 };

    /// <summary>
    /// A small heading of the View panel.
    /// </summary>
    private static TextBlock Heading(string t) => Ui.Heading(t, 12);

    // View, Look: the game's look or plain colours and boxes, see-through buildings, and the 3D
    // resolution (GlView). Presets switch every kind and overlay to the defaults, all on, or only the
    // ground (the web editor's buttons).
    /// <summary>
    /// The game's look, or plain colours and boxes.
    /// </summary>
    internal CheckBox GameLookBox { get; } = new() { Content = Lang.T("view.label.gameLook"), IsChecked = true, FontSize = 12 };

    /// <summary>
    /// Draw buildings see-through.
    /// </summary>
    internal CheckBox SeeThroughBox { get; } = new() { Content = Lang.T("view.label.seeThrough"), FontSize = 12 };

    /// <summary>
    /// The 3D view's resolution: the screen's, balanced, or fast.
    /// </summary>
    internal ComboBox ResolutionBox { get; } = new() { ItemsSource = new[] { Lang.T("view.res.sharp"), Lang.T("view.res.balanced"), Lang.T("view.res.fast") }, SelectedIndex = 0, FontSize = 12, MinWidth = 150 };

    /// <summary>
    /// Every kind, the water and the overlays as they start.
    /// </summary>
    internal Button DefaultsButton { get; } = new() { Content = Lang.T("view.label.defaults"), FontSize = 12 };

    /// <summary>
    /// Every kind, the water and the overlays on.
    /// </summary>
    internal Button AllButton { get; } = new() { Content = Lang.T("view.label.all"), FontSize = 12 };

    /// <summary>
    /// Only the ground: every kind, the water and the overlays off.
    /// </summary>
    internal Button GroundButton { get; } = new() { Content = Lang.T("view.label.ground"), FontSize = 12 };

    /// <summary>
    /// Each preset switch and whether it starts on (the defaults).
    /// </summary>
    private readonly Dictionary<CheckBox, bool> _viewDefaults = [];

    /// <summary>
    /// The View panel's presets.
    /// </summary>
    internal enum ViewPreset { Defaults, All, Ground }

    /// <summary>
    /// Sets every kind, the water and the overlays as the preset says.
    /// </summary>
    internal void ShowPreset(ViewPreset preset)
    {
        foreach (var (box, on) in _viewDefaults)
        {
            box.IsChecked = preset switch { ViewPreset.All => true, ViewPreset.Ground => false, _ => on };
        }
    }

    /// <summary>
    /// Builds the View panel and wires it to the 3D view.
    /// </summary>
    private Border ViewPanel()
    {
        var card = BuildViewPanel();
        WireViewPanel();
        return card;
    }

    /// <summary>
    /// The View panel's overlay switches, each with its tip, as the 3D view shows the overlays now.
    /// </summary>
    /// <param name="list">The panel's list they go into.</param>
    private void AddOverlayBoxes(StackPanel list)
    {
        foreach (var (layer, label) in new[] { (Overlays.Layer.Borders, Lang.T("view.overlay.borders")), (Overlays.Layer.Markers, Lang.T("view.overlay.markers")),
            (Overlays.Layer.Wards, Lang.T("view.overlay.wards")), (Overlays.Layer.Stations, Lang.T("view.overlay.stations")),
            (Overlays.Layer.Flatten, Lang.T("view.overlay.flatten")) })
        {
            var box = new CheckBox { Content = label, IsChecked = View.IsOverlayShown(layer), FontSize = 12, Tag = label };
            ToolTip.SetTip(box, Tips.Overlay(layer));
            _overlayBoxes[layer] = box;
            list.Children.Add(box);
        }
    }

    /// <summary>
    /// Lays out the View panel: its headings, switches, lists and preset buttons with their tips; the switches'
    /// starting states are kept as the presets' defaults.
    /// </summary>
    private Border BuildViewPanel()
    {
        var list = new StackPanel { Spacing = 2 };
        list.Children.Add(Ui.Heading(Lang.T("rail.label.view"), 0));
        list.Children.Add(Heading(Lang.T("view.head.look")));
        GameLookBox.Tip("view.gameLook");
        WaterBox.Tip("view.water");
        SlopeBox.Tip("view.slope");
        ContourBox.Tip("view.contour");
        ContourStepBox.Tip("view.contourStep");
        DefaultsButton.Tip("view.defaults");
        AllButton.Tip("view.all");
        SeeThroughBox.Tip("view.seeThrough");
        ResolutionBox.Tip("view.res3d");
        list.Children.Add(GameLookBox);
        list.Children.Add(SeeThroughBox);
        list.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = Lang.T("view.label.res3d"), FontSize = 12 }, ResolutionBox } });
        list.Children.Add(Heading(Lang.T("view.head.objects")));
        foreach (var k in ObjectKinds.All)
        {
            var box = new CheckBox { Content = ObjectKinds.Label(k), IsChecked = View.IsShown(k), FontSize = 12 };
            ToolTip.SetTip(box, Tips.Kind(k));
            _kindBoxes[k] = box;
            list.Children.Add(box);
        }
        list.Children.Add(WaterBox);
        list.Children.Add(Heading(Lang.T("view.head.overlays")));
        AddOverlayBoxes(list);
        UnsavedBox.Tip("view.unsaved");
        list.Children.Add(UnsavedBox);
        LimitBox.Tip("view.limit");
        list.Children.Add(LimitBox);
        // The presets cover what is shown: kinds, water and overlays (as they are now: the defaults).
        foreach (var box in _kindBoxes.Values.Append(WaterBox).Concat(_overlayBoxes.Values))
        {
            _viewDefaults[box] = box.IsChecked == true;
        }
        GroundButton.Tip("view.ground");
        list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0), Children = { DefaultsButton, AllButton, GroundButton } });
        list.Children.Add(Heading(Lang.T("view.head.building")));
        BuilderBox.Tip("view.builder");
        list.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = Lang.T("view.label.builtBy"), FontSize = 12 }, BuilderBox } });
        list.Children.Add(Heading(Lang.T("view.head.ground")));
        list.Children.Add(SlopeBox);
        list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ContourBox, ContourStepBox, new TextBlock { Text = Lang.T("common.metres"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } });
        list.Children.Add(PlayersControl());
        // The web editor's switches.
        foreach (var box in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(list).OfType<CheckBox>())
        {
            box.Classes.Add("switch");
            box.FontSize = 12.5;
        }
        list.Width = 240;
        return Ui.AsCard(new Border
        {
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            // Scrolls when the window is too short for it (a bar only then).
            Child = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = list },
        });
    }

    /// <summary>
    /// Wires the View panel (built first) to the 3D view: each switch and list sets what is drawn, the presets set
    /// the switches, and the view's counts go into the switches' labels.
    /// </summary>
    private void WireViewPanel()
    {
        GameLookBox.IsCheckedChanged += (_, _) => View.GameLookOn = GameLookBox.IsChecked == true;
        SeeThroughBox.IsCheckedChanged += (_, _) => View.SeeThroughBuildings = SeeThroughBox.IsChecked == true;
        ResolutionBox.SelectionChanged += (_, _) =>
        {
            View.Resolution3D = (GlView.Resolution)Math.Max(0, ResolutionBox.SelectedIndex);
            var (w, h) = View.RenderSize();
            var name = View.Resolution3D switch
            {
                GlView.Resolution.Sharp => Said.Of("view.res.sharp"),
                GlView.Resolution.Balanced => Said.Of("view.res.balanced"),
                _ => Said.Of("view.res.fast"),
            };
            Msg("view.msg.resolution", name, w, h);
        };
        foreach (var (k, box) in _kindBoxes)
        {
            box.IsCheckedChanged += (_, _) => View.SetShown(k, box.IsChecked == true);
        }
        WaterBox.IsCheckedChanged += (_, _) => View.ShowWater = WaterBox.IsChecked == true;
        UnsavedBox.IsCheckedChanged += (_, _) => View.ShowNewMarkers = UnsavedBox.IsChecked == true;
        LimitBox.IsCheckedChanged += (_, _) => View.LimitMarks = LimitBox.IsChecked == true;
        foreach (var (layer, box) in _overlayBoxes)
        {
            box.IsCheckedChanged += (_, _) => View.SetOverlay(layer, box.IsChecked == true);
        }
        DefaultsButton.Click += (_, _) => ShowPreset(ViewPreset.Defaults);
        AllButton.Click += (_, _) => ShowPreset(ViewPreset.All);
        GroundButton.Click += (_, _) => ShowPreset(ViewPreset.Ground);
        View.OverlaysBuilt += o =>
        {
            void Count(Overlays.Layer l, int n) => _overlayBoxes[l].Content = Ui.Counted((string)_overlayBoxes[l].Tag!, n);
            Count(Overlays.Layer.Markers, o.Locations);
            Count(Overlays.Layer.Wards, o.Wards);
            Count(Overlays.Layer.Stations, o.Stations);
            Count(Overlays.Layer.Flatten, o.Flattened);
        };
        BuilderBox.SelectionChanged += async (_, _) => await BuilderChosen();
        SlopeBox.IsCheckedChanged += (_, _) => View.SlopeColours = SlopeBox.IsChecked == true;
        ContourBox.IsCheckedChanged += (_, _) => ShowContours();
        ContourStepBox.SelectionChanged += (_, _) => ShowContours();
        View.KindCounts += counts =>
        {
            foreach (var (k, box) in _kindBoxes)
            {
                box.Content = Ui.Counted(ObjectKinds.Label(k), counts.GetValueOrDefault(k));
            }
        };
    }
}
