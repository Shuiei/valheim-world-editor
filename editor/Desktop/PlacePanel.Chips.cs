using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The favourite and recent kinds, as chips that tick their kinds.
public sealed partial class PlacePanel
{
    /// <summary>The favourite kinds as chips.</summary>
    internal WrapPanel Favourites { get; } = new() { ItemSpacing = 4, LineSpacing = 4 };

    /// <summary>The recently placed kinds as chips.</summary>
    internal WrapPanel Recent { get; } = new() { ItemSpacing = 4, LineSpacing = 4 };

    /// <summary>The favourites and recent sections, hidden while empty.</summary>
    private readonly Control _favBox, _recentBox;

    /// <summary>Ticks every favourite kind.</summary>
    internal Button FavAllButton { get; } = Link(Lang.T("place.label.all"));

    /// <summary>Unticks every favourite kind.</summary>
    internal Button FavNoneButton { get; } = Link(Lang.T("place.label.none"));

    /// <summary>Ticks every recent kind.</summary>
    internal Button RecentAllButton { get; } = Link(Lang.T("place.label.all"));

    /// <summary>Unticks every recent kind.</summary>
    internal Button RecentNoneButton { get; } = Link(Lang.T("place.label.none"));

    /// <summary>The heading of a chip row (in capitals), with its all and none buttons.</summary>
    private static StackPanel ChipHead(string title, Button all, Button none) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 4,
        Margin = new Thickness(0, 6, 0, 2),
        Children = { new TextBlock { Text = title.ToUpperInvariant(), FontSize = 10, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, all, none },
    };

    /// <summary>
    /// The Recent row's kinds: the chosen kinds that are not among them yet, then the ones placed lately (newest
    /// first), so what is chosen always shows.
    /// </summary>
    private List<string> RecentShown() => [.. T.Chosen.Where(n => !Memory.Recent.Contains(n)).Concat(Memory.Recent)];

    /// <summary>
    /// Favourites and recent kinds as buttons: a click ticks or unticks that kind, Shift + click places only it.
    /// </summary>
    private void RenderChips()
    {
        // With no world open, every kind (nothing to check them against).
        var known = Creatable().Select(c => c.Name).ToHashSet();
        bool open = _scene()?.World != null;
        foreach (var (panel, list, box) in new[] { (Favourites, Memory.Favourites, _favBox), (Recent, RecentShown(), _recentBox) })
        {
            panel.Children.Clear();
            foreach (string? n in list.Where(n => !open || known.Contains(n)))
            {
                var b = new ToggleButton { Content = Plain(n), Tag = n, IsChecked = T.Chosen.Contains(n), FontSize = 11, Padding = new Thickness(8, 3) }.Tip(ReferenceEquals(panel, Favourites) ? "place.favourite" : "place.recent");
                b.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, e) => _shiftPress = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
                b.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, _) => _shiftPress = false, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
                b.Click += (_, _) =>
                {
                    bool shift = _shiftPress;
                    _shiftPress = false;
                    // The button turned itself on or off: what is ticked decides.
                    b.IsChecked = T.Chosen.Contains(n);
                    ChipClicked(n, shift);
                };
                panel.Children.Add(b);
            }
            box.IsVisible = panel.Children.Count > 0;
        }
    }

    /// <summary>A chip clicked: ticks or unticks that kind; with Shift, places only it.</summary>
    internal void ChipClicked(string name, bool shift) => Choose(name, add: !shift);
}
