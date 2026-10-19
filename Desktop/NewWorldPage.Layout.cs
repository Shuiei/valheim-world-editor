using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The page's layout: the cards of the world, the search and the found seeds, the map with its zoom and switches, and the lists beside it.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    internal static DockPanel Row(string label, Control c) => new() { Children = { new TextBlock { Text = label, Width = 90, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, c } };
    internal static DockPanel Metres(string label, NumericUpDown n)
    {
        var unit = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { n, new TextBlock { Text = Lang.T("common.metres"), VerticalAlignment = VerticalAlignment.Center } } };
        return new DockPanel { Children = { new TextBlock { Text = label, Width = 120, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, unit } };
    }

    internal static Button ZoomButton(string text, string tip) => new Button { Content = text, FontSize = 13, Width = 30, Height = 30, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center }.Tip(tip);

    /// <summary>The world card: the name, the seed with Generate and Roll, the folder, and Create.</summary>
    /// <returns>The card.</returns>
    private Border BuildWorldCard()
    {
        var seedRow = new DockPanel { Children = { new TextBlock { Text = Lang.T("common.seed"), Width = 90, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Generate, Roll, SeedBox } };
        DockPanel.SetDock(Generate, Dock.Right);
        DockPanel.SetDock(Roll, Dock.Right);
        Generate.Margin = Roll.Margin = new Thickness(6, 0, 0, 0);
        return Ui.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = Lang.T("newWorld.head.world"), FontSize = 15, FontWeight = FontWeight.SemiBold },
                Row(Lang.T("dialog.label.name"), NameBox),
                seedRow,
                Row(Lang.T("newWorld.label.savedIn"), Where),
                Ui.Hint(Lang.T("newWorld.hint.normalWorld")),
                Generation,
                Create,
                Problem,
            },
        });
    }

    /// <summary>The search card: the wishes, the Find and Stop buttons, and the progress.</summary>
    /// <returns>The card.</returns>
    private Border BuildSearchCard()
    {
        var findRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Count, Find, Stop } };
        return Ui.Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = Lang.T("newWorld.head.find"), FontSize = 15, FontWeight = FontWeight.SemiBold },
                Ui.Hint(Lang.T("newWorld.hint.search")),
                BigLand, LandAround, OnFoot,
                Metres(Lang.T("newWorld.label.swampWithin"), Swamp),
                Metres(Lang.T("newWorld.label.mountainWithin"), Mountain),
                Metres(Lang.T("newWorld.label.plainsWithin"), Plains),
                Metres(Lang.T("newWorld.label.haldorWithin"), Haldor),
                Metres(Lang.T("newWorld.label.bogWitchWithin"), BogWitch),
                Metres(Lang.T("newWorld.label.hildirWithin"), Hildir),
                Metres(Lang.T("newWorld.label.bossesWithin"), Bosses),
                findRow,
                Progress,
            },
        });
    }

    /// <summary>The map's zoom buttons: in, out and fit.</summary>
    /// <returns>The buttons, in a column over the map.</returns>
    private StackPanel BuildZoom()
    {
        var zoomIn = ZoomButton("+", "newWorld.zoomIn");
        var zoomOut = ZoomButton("−", "newWorld.zoomOut");
        var fit = ZoomButton("⤢", "newWorld.fit");
        zoomIn.Click += (_, _) => Map.Zoom(1 / 1.6);
        zoomOut.Click += (_, _) => Map.Zoom(1.6);
        fit.Click += (_, _) => Map.Fit();
        return new StackPanel { Spacing = 4, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Children = { zoomIn, zoomOut, fit } };
    }

    /// <summary>The map's switches: the landmarks and the dungeon kinds, in a panel at the top right of the map.</summary>
    /// <returns>The panel.</returns>
    private Border BuildToggles()
    {
        var toggles = new Border
        {
            Background = Ui.Panel,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10),
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = Lang.T("newWorld.head.onMap"), FontSize = 12, FontWeight = FontWeight.SemiBold },
                    ShowStart, ShowBosses, ShowHaldor, ShowBogWitch, ShowHildir,
                    new TextBlock { Text = Lang.T("newWorld.head.dungeons"), FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) },
                },
            },
        };
        foreach (var t in ShowDungeons.Values)
        {
            ((StackPanel)toggles.Child!).Children.Add(t);
        }
        return toggles;
    }

    /// <summary>
    /// Lays out the page: the title row, the search and found seeds on the left, and the map and its lists on the right.
    /// </summary>
    /// <param name="back">The Start button, at the top left.</param>
    /// <returns>The page.</returns>
    private Grid BuildPage(Button back)
    {
        var world = BuildWorldCard();
        var search = BuildSearchCard();
        Found.Children.Add(Ui.Hint(Lang.T("newWorld.hint.found")));
        var results = new ScrollViewer { Content = Found, Height = 330 };
        var found = Ui.Card(new StackPanel
        {
            Spacing = 6,
            Children = { new TextBlock { Text = Lang.T("newWorld.head.found"), FontSize = 15, FontWeight = FontWeight.SemiBold }, results },
        });
        // At the top, not stretched to the window: its height is what it holds.
        var column = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Top, Children = { world, search, found } };
        var left = new ScrollViewer { Content = column, Padding = new Thickness(0, 0, 10, 0) };
        WireResults(results, column, left);
        var toggles = BuildToggles();
        var zoom = BuildZoom();
        var map = new Border
        {
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = new Panel { Children = { Map, Marks, Readout, toggles, zoom, Loading } },
        };
        var info = new ScrollViewer { Content = Info, Padding = new Thickness(0, 0, 8, 0) };
        var right = new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,320") };
        right.Children.Add(map);
        Grid.SetColumn(info, 2);
        right.Children.Add(info);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("470,14,*") };
        body.Children.Add(left);
        Grid.SetColumn(right, 2);
        body.Children.Add(right);
        var page = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(20), RowSpacing = 12 };
        page.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { back, new TextBlock { Text = Lang.T("start.page.new"), FontSize = 20, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } });
        Grid.SetRow(body, 1);
        page.Children.Add(body);
        return page;
    }

    /// <summary>The loading sign over the map: a line of what is being done, and a bar moving while it is.</summary>
    /// <returns>The sign, hidden until a seed is looked at.</returns>
    private Border BuildLoadingSign()
    {
        return new Border
        {
            Background = Ui.Panel,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            IsHitTestVisible = false,
            Child = new StackPanel { Spacing = 8, Children = { LoadingText, new ProgressBar { IsIndeterminate = true, Width = 220, Height = 4, MinHeight = 4 } } },
        };
    }
}
