using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
        Search.Tip("blueprints.search");
        close.Click += (_, _) => { Card!.IsVisible = false; Closed?.Invoke(); };
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { Render(); } };
        ImportButton.Tip("blueprints.import");
        ImportButton.Click += async (_, _) => { if (await PickFile() is string path) { Import(path); } };
        NewButton.Tip("blueprints.new");
        NewButton.Click += (_, _) => EditAsked?.Invoke(null);
        GetHomesteadButton.Tip("blueprints.getHomestead");
        GetHomesteadButton.Click += async (_, _) => await OpenUrl(new Uri(Homestead.PageUrl));
        _bannerBox = new Border
        {
            CornerRadius = new CornerRadius(6),
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
                    new ScrollViewer { MaxHeight = 640, Content = List },
                    _folder,
                },
            },
        });
    }

    /// <summary>A box searching blueprints (here and in the Workshop's Library).</summary>
    internal static TextBox SearchBox()
    {
        return new TextBox { PlaceholderText = Lang.T("blueprints.hint.search"), FontSize = 12 };
    }

    /// <summary>Import file… (here and in the Workshop's Library).</summary>
    internal static Button ImportFileButton()
    {
        return new Button { Content = Lang.T("blueprints.label.import"), FontSize = 12 };
    }
}
