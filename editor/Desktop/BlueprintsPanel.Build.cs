using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>Builds the panel (hidden).</summary>
    /// <param name="paste">The paste tool whose clipboard is saved and filled.</param>
    /// <param name="scene">The open world's scene, null when none is open.</param>
    public BlueprintsPanel(PasteTool paste, Func<WorldScene?> scene)
    {
        _paste = paste;
        _scene = scene;
        var close = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(6, 0) }.Tip("card.close");
        close.Click += (_, _) => { Card!.IsVisible = false; Closed?.Invoke(); };
        Library.Folder = () => Status.Folder;
        Library.Older = () => Store;
        Library.Models = () => Models();
        Library.PlaceAsked += b => Paste(b.Id);
        Library.OpenAsked += b => EditAsked?.Invoke(b.Entry!.Path);
        Library.DetailsAsked += async b => await EditDetails(b.Entry!);
        Library.ExportAsked += b => Export(b.Id, "vbuild");
        Library.DeleteAsked += async b => await Delete(b.Id, b.Name);
        Library.ToHomesteadAsked += b => ToHomestead(b.Id);
        ImportButton.Click += async (_, _) => { if (await PickFile() is string path) { Import(path); } };
        NewButton.Click += (_, _) => EditAsked?.Invoke(null);
        PasteClipboardButton.Tip("blueprints.pasteClipboard");
        PasteClipboardButton.Click += (_, _) => StartPasting();
        SaveClipboardButton.Tip("blueprints.saveClipboard");
        SaveClipboardButton.Click += async (_, _) => await Save();
        _paste.Changed += ShowClipboard;
        ShowClipboard();
        GetHomesteadButton.Tip("blueprints.getHomestead");
        GetHomesteadButton.Click += async (_, _) => await OpenUrl(new Uri(Homestead.PageUrl));
        _bannerBox = new Border
        {
            CornerRadius = Ui.ControlRadius,
            Padding = new Thickness(8, 6),
            Child = new StackPanel { Spacing = 4, Children = { Banner, GetHomesteadButton } },
        };
        Card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            IsVisible = false,
            Child = new StackPanel
            {
                Width = 340,
                Spacing = 6,
                Children =
                {
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { Ui.Title(Lang.T("rail.label.blueprints"), HelpTopics.Blueprints), Ui.Col(close, 1) } },
                    _bannerBox,
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { Search, Ui.Col(ImportButton, 1) } },
                    NewButton,
                    ClipboardRow(),
                    new ScrollViewer { MaxHeight = 600, Content = List },
                    _folder,
                },
            },
        });
    }

    /// <summary>
    /// The clipboard, shown as the library's first row: what it holds, Paste and Save as blueprint… (off while it is
    /// empty).
    /// </summary>
    private Border ClipboardRow() => new()
    {
        Padding = new Thickness(6),
        BorderBrush = Ui.Line,
        BorderThickness = new Thickness(1),
        CornerRadius = Ui.ControlRadius,
        Child = new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = Lang.T("blueprints.clip.head"), FontSize = 12, FontWeight = FontWeight.SemiBold },
                ClipInfo,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { PasteClipboardButton, SaveClipboardButton } },
            },
        },
    };

    /// <summary>Shows what the clipboard holds, its two buttons on only while it holds something.</summary>
    private void ShowClipboard()
    {
        var c = _paste.Clip;
        ClipInfo.Text = c == null ? Lang.T("blueprints.clip.none")
            : c.Name != null ? Lang.T("blueprints.clip.named", c.Name, c.W, c.H, c.Objects.Count) : Lang.T("blueprints.clip.holds", c.W, c.H, c.Objects.Count);
        PasteClipboardButton.IsEnabled = SaveClipboardButton.IsEnabled = c != null;
    }
}
