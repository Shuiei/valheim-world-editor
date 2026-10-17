using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>One row: picture, name, what it holds, its buttons.</summary>
    /// <param name="picture">The picture, null: none.</param>
    /// <param name="name">The blueprint's name.</param>
    /// <param name="meta">What it holds, where from.</param>
    /// <param name="buttons">Adds the row's buttons through the function it is given (text, tip key, click).</param>
    /// <param name="cost">What it costs to build, shown under the meta line (null: not shown).</param>
    /// <param name="description">Shown in up to three lines (null or blank: not shown).</param>
    /// <param name="tags">Shown as chips (null or none: not shown).</param>
    private Border Row(Bitmap? picture, string name, string meta, Action<Func<string, string, Action, Button>> buttons, string? cost = null, string? description = null, IReadOnlyList<string>? tags = null)
    {
        var acts = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
        Button Act(string text, string tip, Action a)
        {
            // By its tip key, which Claude knows it by (and is kept from the ones that write or delete files).
            var btn = new Button { Content = text, FontSize = 11, Padding = new Thickness(6, 1) }.Tip(tip);
            btn.Click += (_, _) => a();
            acts.Children.Add(btn);
            return btn;
        }
        buttons(Act);
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = name, FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = meta, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap });
        if (cost != null)
        {
            text.Children.Add(new TextBlock { Text = cost, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(214, 190, 140)), TextWrapping = TextWrapping.Wrap });
        }
        if (!string.IsNullOrWhiteSpace(description))
        {
            text.Children.Add(new TextBlock { Text = description, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis });
        }
        if (tags is { Count: > 0 })
        {
            var chips = new WrapPanel { ItemSpacing = 3, LineSpacing = 3 };
            foreach (string tag in tags)
            {
                chips.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(50, 120, 160, 220)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 0),
                    Child = new TextBlock { Text = tag, FontSize = 10.5 },
                });
            }
            text.Children.Add(chips);
        }
        text.Children.Add(acts);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("80,*"),
            ColumnSpacing = 8,
            Children =
            {
                new Image { Source = picture, Width = 80, Height = 80, VerticalAlignment = VerticalAlignment.Top },
                Ui.Col(text, 1),
            },
        };
        return new Border { Padding = new Thickness(4), CornerRadius = new CornerRadius(6), Child = grid };
    }
}
