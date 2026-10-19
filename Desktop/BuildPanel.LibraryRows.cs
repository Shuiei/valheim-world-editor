using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>Lists the blueprints in the folder that match the search.</summary>
    /// <param name="again">Reads the folder again; false (the search changed): the blueprints read last are searched.</param>
    internal void RenderLibrary(bool again = true)
    {
        LibraryList.Children.Clear();
        string folder = Folder();
        if (again || _libraryRead is not { } read || read.Folder != folder)
        {
            _libraryRead = read = (folder, Homestead.List(folder));
        }
        var all = read.Entries;
        string q = LibrarySearch.Text?.Trim() ?? "";
        var list = all.Where(e => e.Matches(q)).ToList();
        if (list.Count == 0)
        {
            LibraryList.Children.Add(new TextBlock
            {
                Text = all.Count == 0 ? Lang.T("build.libraryEmpty") : Lang.T("blueprints.noMatch"),
                FontSize = 11,
                Foreground = Ui.Muted,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }
        foreach (var e in list)
        {
            LibraryList.Children.Add(LibraryRow(e));
        }
    }

    /// <summary>
    /// A Library row's second line: the pieces and the cost, and how many pieces only the editor can place when the file
    /// says.
    /// </summary>
    private static string LibraryRowText(Homestead.Entry e)
    {
        string text = Lang.T("build.libraryRow", e.Pieces, Homestead.Cost(e.Kinds).Describe(3));
        return e.EditorOnlyNote is { } note ? text + " · " + note.Shown : text;
    }

    /// <summary>
    /// One blueprint in the Library: its picture (drawn from its pieces when it has none), name, pieces and cost,
    /// Open and Add; dragged a few pixels it is dropped onto the plot.
    /// </summary>
    private Border LibraryRow(Homestead.Entry e)
    {
        Bitmap? pic = BlueprintPictures.File(e.Picture);
        var open = new Button { Content = Lang.T("common.open"), FontSize = 11, Padding = new Thickness(6, 1) }.Tip("build.open");
        var add = new Button { Content = Lang.T("common.add"), FontSize = 11, Padding = new Thickness(6, 1) }.Tip("build.add");
        open.Click += (_, _) => OpenAsked?.Invoke(e.Path);
        add.Click += (_, _) => AddAsked?.Invoke(e.Path);
        var image = new Image { Source = pic, Width = 60, Height = 60, VerticalAlignment = VerticalAlignment.Top };
        if (pic == null)
        {
            DrawPicture(e.Path, image);
        }
        var row = new Border
        {
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = e.Path,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("60,*"),
                ColumnSpacing = 8,
                Children =
                {
                    image,
                    Ui.Col(new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = e.Name, FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = LibraryRowText(e), FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
                            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { open, add } },
                        },
                    }, 1),
                },
            },
        };
        ToolTip.SetTip(row, Lang.T("build.dragTip"));
        // Dragged a few pixels: a drag and drop carrying the blueprint's file.
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
            data.Add(DataTransferItem.Create(BlueprintFormat, e.Path));
            await DragDrop.DoDragDropAsync(p, data, DragDropEffects.Copy);
        };
        return row;
    }
}
