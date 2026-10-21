using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>
    /// Blueprints with their pictures decoded at twice the tiles' width (sharp on high-density screens, not full size);
    /// a picture missing or unreadable is null. Run away from the window's thread.
    /// </summary>
    private static List<(Homestead.Entry Entry, Bitmap? Picture)> Pictured(IEnumerable<Homestead.Entry> blueprints)
    {
        var pictured = new List<(Homestead.Entry, Bitmap?)>();
        foreach (var b in blueprints)
        {
            Bitmap? picture = null;
            if (b.Picture != null && File.Exists(b.Picture))
            {
                try
                {
                    using var s = File.OpenRead(b.Picture);
                    picture = Bitmap.DecodeToWidth(s, 440);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
                {
                    // The kind's icon instead.
                    Log.CaughtBrief(e, "decoding a blueprint's picture");
                }
            }
            pictured.Add((b, picture));
        }
        return pictured;
    }

    /// <summary>Disposes the pictures of blueprints that are not shown (a fill overtaken by a later one).</summary>
    private static void Dispose(List<(Homestead.Entry Entry, Bitmap? Picture)> unshown)
    {
        foreach (var (_, picture) in unshown)
        {
            picture?.Dispose();
        }
    }

    /// <summary>Disposes the pictures tiles showed until now and holds the new tiles' ones instead.</summary>
    private static void Keep(List<Bitmap> held, List<(Homestead.Entry Entry, Bitmap? Picture)> now)
    {
        foreach (var b in held)
        {
            b.Dispose();
        }
        held.Clear();
        foreach (var (_, picture) in now)
        {
            if (picture != null)
            {
                held.Add(picture);
            }
        }
    }

    /// <summary>A tile: a picture (or the kind's icon on its colour), the name, a line under it.</summary>
    /// <param name="picture">A picture; null shows the icon instead.</param>
    /// <param name="icon">The kind's icon.</param>
    /// <param name="tint">The icon's background colour.</param>
    /// <param name="name">The name, in bold.</param>
    /// <param name="line">The line under it.</param>
    /// <param name="tip">The tile's tooltip.</param>
    /// <param name="open">What a click does.</param>
    private static Button Tile(Bitmap? picture, string icon, Color tint, string name, string line, string tip, Action open)
    {
        Control top = picture != null ? new Image { Source = picture, Stretch = Stretch.UniformToFill }
            : new Border { Background = new SolidColorBrush(tint), Child = new Viewbox { Width = 30, Height = 30, Child = Icons.Make(icon, 30), Opacity = 0.7 } };
        var tile = new Button
        {
            Width = 220,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = new StackPanel
            {
                Children =
                {
                    new Border { Height = 100, ClipToBounds = true, CornerRadius = new CornerRadius(6, 6, 0, 0), Child = top },
                    new StackPanel
                    {
                        Margin = new Thickness(12, 10),
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = line, FontSize = 12, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis },
                        },
                    },
                },
            },
        };
        ToolTip.SetTip(tile, tip);
        tile.Click += (_, _) => open();
        return tile;
    }

    /// <summary>A date as "today", "yesterday", "n days ago" (under two weeks) or the date itself.</summary>
    private static string Ago(DateTime when)
    {
        int days = (DateTime.Now.Date - when.Date).Days;
        return days <= 0 ? Lang.T("start.ago.today") : days == 1 ? Lang.T("start.ago.yesterday") : days < 14 ? Lang.T("start.ago.days", days) : Lang.T("start.ago.date", when);
    }
}
