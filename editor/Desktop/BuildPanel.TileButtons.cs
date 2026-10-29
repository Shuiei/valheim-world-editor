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

    /// <summary>A heading above a group of tiles.</summary>
    /// <param name="heading">The heading's text.</param>
    private static TextBlock HeadingBlock(string heading) => new() { Text = heading, Width = 320, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Ui.Muted, Margin = new Thickness(2, 4, 0, 0) };

    /// <summary>A piece's tile: its picture (drawn when not yet), its name, its tip with its cost, and a click to choose it.</summary>
    /// <param name="prefab">The piece's prefab name.</param>
    /// <param name="name">The piece's English name.</param>
    /// <param name="chosen">The piece the Place tool puts down now, null when none.</param>
    /// <returns>The tile.</returns>
    private Button TileButton(string prefab, string name, string? chosen)
    {
        var image = new Image { Width = 64, Height = 64, Source = Picture(prefab) };
        if (image.Source == null)
        {
            if (!_waiting.TryGetValue(prefab, out var images))
            {
                _waiting[prefab] = images = [];
            }
            images.Add(image);
        }
        var b = new Button
        {
            Tag = prefab,
            Width = 76,
            Padding = new Thickness(2),
            Content = new StackPanel
            {
                Children =
                {
                    image,
                    new TextBlock { Text = name, FontSize = 10, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis },
                },
            },
        };
        ToolTip.SetTip(b, $"{name} ({prefab}) · {PieceCost.Of([prefab]).Describe(6)}");
        b.Classes.Set("on", prefab == chosen);
        b.Click += (_, _) => Choose(prefab);
        return b;
    }
}
