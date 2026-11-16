using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

internal sealed partial class BlueprintLibrary
{
    /// <summary>One row: picture, name, what it holds, its cost, description and tags, its buttons.</summary>
    /// <param name="item">The blueprint; the row's Tag is its file (a Homestead one) or its id (an older one).</param>
    /// <param name="picture">The picture, null: none (yet).</param>
    /// <param name="meta">What it holds, where from.</param>
    /// <param name="buttons">Adds the row's buttons through the function it is given (text, tip key, click).</param>
    /// <param name="cost">What it costs to build, shown under the meta line (null: not shown).</param>
    /// <param name="description">Shown in up to three lines (null or blank: not shown).</param>
    /// <param name="tags">Shown as chips (null or none: not shown).</param>
    private Border Row(Item item, Bitmap? picture, string meta, Action<Action<string, string, Action>> buttons, string? cost = null, string? description = null, IReadOnlyList<string>? tags = null)
    {
        var acts = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
        void Act(string text, string tip, Action a)
        {
            // By its tip key, which Claude knows it by (and is kept from the ones that write or delete files).
            var btn = new Button { Content = text, FontSize = 11, Padding = new Thickness(6, 1) }.Tip(tip);
            btn.Click += (_, _) => a();
            acts.Children.Add(btn);
        }
        buttons(Act);
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = item.Name, FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = meta, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap });
        if (cost != null)
        {
            text.Children.Add(new TextBlock { Text = cost, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        }
        if (!string.IsNullOrWhiteSpace(description))
        {
            text.Children.Add(new TextBlock { Text = description, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis });
        }
        if (tags is { Count: > 0 })
        {
            var chips = new WrapPanel { ItemSpacing = 3, LineSpacing = 3 };
            foreach (string tag in tags)
            {
                chips.Children.Add(new Border
                {
                    Background = Ui.Hover,
                    BorderBrush = Ui.Line2,
                    BorderThickness = new Thickness(1),
                    CornerRadius = Ui.ControlRadius,
                    Padding = new Thickness(6, 0),
                    Child = new TextBlock { Text = tag, FontSize = 10.5 },
                });
            }
            text.Children.Add(chips);
        }
        text.Children.Add(acts);
        return new Border
        {
            Width = RowWidth,
            Padding = new Thickness(6),
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = Ui.ControlRadius,
            Background = Brushes.Transparent,
            Tag = item.Entry?.Path ?? item.Id,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 8,
                Children =
                {
                    new Image { Source = picture, Width = PictureSize, Height = PictureSize, VerticalAlignment = VerticalAlignment.Top },
                    Ui.Col(text, 1),
                },
            },
        };
    }

    /// <summary>A row's picture.</summary>
    private static Image PictureOf(Border row) => row.GetLogicalDescendants().OfType<Image>().First();

    /// <summary>
    /// Makes a row one that is dragged onto the plot: pressed (not on one of its buttons) and moved a few pixels, a
    /// drag and drop carrying the blueprint's file starts.
    /// </summary>
    private static void Draggable(Border row, string path)
    {
        row.Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(row, Lang.T("build.dragTip"));
        PointerPressedEventArgs? pressed = null;
        row.PointerPressed += (_, ev) =>
        {
            if (ev.GetCurrentPoint(row).Properties.IsLeftButtonPressed && ev.Source is not Button)
            {
                pressed = ev;
            }
        };
        row.PointerReleased += (_, _) => pressed = null;
        row.PointerMoved += async (_, ev) =>
        {
            if (pressed is not { } p || Point.Distance(p.GetPosition(row), ev.GetPosition(row)) < 6)
            {
                return;
            }
            pressed = null;
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(DragFormat, path));
            await DragDrop.DoDragDropAsync(p, data, DragDropEffects.Copy);
        };
    }
}
