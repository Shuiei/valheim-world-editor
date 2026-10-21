using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The side panel: the title, the live and waiting-changes boxes, the options, the picked spot, the search, the zones and the edited zones.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>Builds the side panel over the map.</summary>
    /// <param name="worlds">The Worlds button, shown in the title row.</param>
    /// <param name="search">The search section.</param>
    /// <param name="zones">The zones section.</param>
    /// <returns>The panel.</returns>
    private Border BuildSide(Button worlds, Expander search, Expander zones)
    {
        return new Border
        {
            Background = PanelBg,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = Ui.Pad,
            Margin = new Thickness(10),
            Width = 340,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { Ui.BrandTitle(16, VerticalAlignment.Center), Col(worlds, 1) } },
                        Meta,
                        _liveBox,
                        _changesBox,
                        H2(Lang.T("top.label.map")),
                        new StackPanel { Children = { BuildingsBox, RuinsBox, EditedBox, PaintBox, GridBox, CloudsBox } },
                        _pickBox,
                        new TextBlock { Text = Lang.T("map.hint.click"), FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
                        search,
                        zones,
                        _editedSection,
                    },
                },
            },
        };
    }
}
