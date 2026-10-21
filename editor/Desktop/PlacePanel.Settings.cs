using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The elevation, snapping, beside or on top, and grow room settings.
public sealed partial class PlacePanel
{
    /// <summary>Where objects stand, in <c>PlaceTool.Elevations</c> order.</summary>
    internal ComboBox ElevationBox { get; } = new() { ItemsSource = new[] { Lang.T("place.elevation.ground"), Lang.T("place.elevation.above"), Lang.T("place.elevation.at") }, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };

    /// <summary>The height above the ground, or the one height for all, in metres.</summary>
    internal NumericUpDown ElevBox { get; } = new() { Value = 0, Increment = 0.5m, FormatString = "0.0#", FontSize = 12 };

    /// <summary>Snap pieces to the pieces already there (shown when pieces are chosen).</summary>
    internal CheckBox SnapToBox { get; } = new() { Content = Lang.T("place.label.snapTo"), IsChecked = true, FontSize = 12 };

    /// <summary>Brush: snap a single piece beside the nearest one.</summary>
    internal Button BesideButton { get; } = new() { Content = Lang.T("place.label.beside"), FontSize = 12 };

    /// <summary>Brush: snap a single piece on top of the one under the cursor.</summary>
    internal Button OnTopButton { get; } = new() { Content = Lang.T("place.label.onTop"), FontSize = 12 };

    /// <summary>Leave saplings and crops room to grow (shown when one is chosen).</summary>
    internal CheckBox GrowBox { get; } = new() { Content = Lang.T("place.label.grow"), IsChecked = true, FontSize = 12 };

    /// <summary>The kinds: the chooser, the pick and untick, the favourites and recent kinds, and the elevation, snap and grow switches.</summary>
    private void WireKinds(PlaceInput input, PlaceTool t)
    {
        KindsButton.Click += (_, _) => { Chooser!.IsVisible = !Chooser.IsVisible; KindsButton.Content = Chooser.IsVisible ? DoneText : ChooseKindsText; FillList(); };
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { FillList(); } };
        PickButton.Click += (_, _) =>
        {
            input.PickOnce = (name, shift) =>
            {
                if (!Creatable().Any(c => c.Name == name))
                {
                    Say(Said.Of("select.msg.cannotPlace", name));
                    return;
                }
                // A click places only that kind; Shift + click adds it to the ticked kinds.
                Choose(name, add: shift);
                Say(Said.Of("place.msg.placing", string.Join(", ", T.Chosen)));
            };
            Say(Said.Of("place.msg.pickHow"));
        };
        UntickAllButton.Tip("place.untickAll");
        UntickAllButton.Click += (_, _) => { T.Chosen.Clear(); Chosen(); };
        FavAllButton.Tip("place.favAll");
        FavNoneButton.Tip("place.favNone");
        RecentAllButton.Tip("place.recentAll");
        RecentNoneButton.Tip("place.recentNone");
        FavAllButton.Click += (_, _) => TickRow(Memory.Favourites, true);
        FavNoneButton.Click += (_, _) => TickRow(Memory.Favourites, false);
        RecentAllButton.Click += (_, _) => TickRow(RecentShown(), true);
        RecentNoneButton.Click += (_, _) => TickRow(RecentShown(), false);
        ElevationBox.SelectionChanged += (_, _) => { if (!_filling) { t.Elevation = (PlaceTool.Elevations)Math.Max(0, ElevationBox.SelectedIndex); t.Notify(); } Sync(); };
        ElevBox.ValueChanged += (_, e) => { if (!_filling) { t.Elev = (float)(e.NewValue ?? 0); t.Notify(); } };
        SnapToBox.IsCheckedChanged += (_, _) => { t.SnapTo = SnapToBox.IsChecked == true; t.Notify(); };
        BesideButton.Click += (_, _) => { t.OnTop = false; Sync(); t.Notify(); };
        OnTopButton.Click += (_, _) => { t.OnTop = true; Sync(); t.Notify(); };
        BesideButton.Tip("place.beside");
        OnTopButton.Tip("place.onTop");
        GrowBox.IsCheckedChanged += (_, _) => { t.GrowRoom = GrowBox.IsChecked == true; t.Notify(); };
    }

    /// <summary>A warning: nothing to place yet, or chosen kinds that only grow on cultivated ground.</summary>
    internal TextBlock Note { get; } = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(240, 190, 90)), TextWrapping = TextWrapping.Wrap };
}
