using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The panel's layout: the dungeon rows, the generator's expander and the card the window shows.
/// </summary>
public sealed partial class DungeonPanel
{
    /// <summary>
    /// Builds the panel's card: the dungeon rows, the generator's expander and the go-to buttons.
    /// </summary>
    /// <returns>The dungeon rows, the generator's expander, the go-to row and the card.</returns>
    private (StackPanel Body, Expander Generate, StackPanel Go, Control Card) BuildCard()
    {
        var body = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                DungeonBox,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { GoInside, GoOutside, BuildHere } },
                new StackPanel { Spacing = 2, Children = { _cutText, CutSlider } },
                new TextBlock { Text = Lang.T("dungeon.head.rooms"), FontSize = 11, Foreground = Ui.Muted, FontWeight = FontWeight.SemiBold },
                Filter, Rooms, Contents, Doors,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { TurnButton, DeleteButton } },
                CloseEnds, Info,
            },
        };
        var generate = new Expander
        {
            Header = new TextBlock { Text = Lang.T("build.head.generate"), FontSize = 12, FontWeight = FontWeight.SemiBold },
            Content = Gen.View,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsExpanded = true,
        };
        generate.Tip("dungeon.gen");
        var go = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, IsVisible = false, Children = { Gen.ToDungeon, Gen.ToEntrance } };
        Gen.PlacedChanged += yes => go.IsVisible = yes;
        var card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new ScrollViewer
            {
                MaxHeight = 820,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = new StackPanel
                {
                    Width = 280,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = Lang.T("rail.label.dungeon"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                        generate, go,
                        new TextBlock { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                            Text = Lang.T("dungeon.hint.editing") },
                        _empty, body,
                    },
                },
            },
        });
        return (body, generate, go, card);
    }
}
