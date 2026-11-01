using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Terrain;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>The heading a piece's tile sits under: its mod's group (on the Mods tab) and its station, or the piece's own group.</summary>
    /// <param name="prefab">The piece's prefab name.</param>
    internal static string StationHeading(string prefab)
    {
        string? station = PieceCost.Get(prefab)?.Station;
        string mod = ModGroup(prefab);
        return (mod.Length > 0 ? mod + " · " : "") + (PieceGroup(prefab) ?? station ?? Lang.T("build.noStation"));
    }

    /// <summary>A tile's width and its picture's, in pixels.</summary>
    internal const double TileWidth = 96, PictureSize = 76;

    /// <summary>
    /// A heading before a group of tiles, in the row with them: a narrow upright label as tall as a tile, so the groups
    /// follow each other along the dock.
    /// </summary>
    /// <param name="heading">The heading's text.</param>
    private static TextBlock HeadingBlock(string heading) => new()
    {
        Text = heading,
        Width = 84,
        MinHeight = PictureSize + 30,
        Padding = new Thickness(8, 6),
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = Ui.Muted,
        Background = Ui.Panel2,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
    };

    /// <summary>A piece's tile: its picture (drawn when not yet), its name, its tip with its cost, and a click to choose it.</summary>
    /// <param name="prefab">The piece's prefab name.</param>
    /// <param name="name">The piece's English name.</param>
    /// <param name="chosen">The piece the Place tool puts down now, null when none.</param>
    /// <returns>The tile.</returns>
    private Button TileButton(string prefab, string name, string? chosen)
    {
        var image = PictureImage(prefab, PictureSize);
        var b = new Button
        {
            Tag = prefab,
            Width = TileWidth,
            Padding = new Thickness(3),
            Content = new StackPanel
            {
                Children =
                {
                    image,
                    new TextBlock { Text = name, FontSize = 10.5, Height = 30, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis },
                },
            },
        };
        ToolTip.SetTip(b, $"{name} ({prefab}) · {PieceCost.Of([prefab]).Describe(6)}");
        b.Classes.Set("on", prefab == chosen);
        b.Click += (_, _) => Pick(prefab);
        return b;
    }

    /// <summary>An image of a piece's picture, filled in when the picture is drawn when it is not yet.</summary>
    /// <param name="prefab">The piece's prefab name.</param>
    /// <param name="size">The image's width and height, in pixels.</param>
    private Image PictureImage(string prefab, double size)
    {
        var image = new Image { Width = size, Height = size, Source = Picture(prefab) };
        if (image.Source == null)
        {
            if (!_waiting.TryGetValue(prefab, out var images))
            {
                _waiting[prefab] = images = [];
            }
            images.Add(image);
        }
        return image;
    }
}
