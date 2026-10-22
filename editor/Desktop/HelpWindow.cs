using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Help window: the documentation carried in the app (HelpDocs), beside the editor and never in its way. On the
/// left, the search box and the contents (the first page's list, the page shown with its parts under it), or what a
/// search found; on the right, the page, with Back, Forward, Home and Open in browser above it. One window for the
/// whole app: asking for help again shows the place asked for in it.
/// </summary>
public sealed partial class HelpWindow : Window
{
    /// <summary>The widest the page's text runs (wider lines are hard to read).</summary>
    private const double PageWidth = 860;

    /// <summary>The window, while it is open.</summary>
    public static HelpWindow? Current { get; private set; }

    /// <summary>The search box: typing in it lists what it finds instead of the contents.</summary>
    internal TextBox SearchBox { get; } = new() { PlaceholderText = Lang.T("docs.search"), Margin = new Thickness(12, 12, 12, 6) };

    /// <summary>The contents, or what the search found.</summary>
    internal StackPanel Side { get; } = new() { Spacing = 1, Margin = new Thickness(8, 0, 8, 12) };

    /// <summary>The page shown, scrolled.</summary>
    internal ScrollViewer Scroller { get; } = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

    /// <summary>The place shown.</summary>
    internal HelpTarget Shown { get; private set; } = HelpTarget.Home;

    /// <summary>The page shown's drawing (its headings by id).</summary>
    internal HelpView? View { get; private set; }

    /// <summary>Back: the place shown before.</summary>
    internal Button BackButton { get; } = Bar("back", "docs.back");

    /// <summary>Forward: the place Back left.</summary>
    internal Button ForwardButton { get; } = Bar("forward", "docs.forward");

    /// <summary>Home: the first page.</summary>
    internal Button HomeButton { get; } = Bar("home", "docs.home");

    /// <summary>Open in browser: the page shown, on GitHub.</summary>
    internal Button BrowserButton { get; } = Bar("external", "docs.browser");

    /// <summary>A picture shown full size over the page (a click on one); a click or Esc closes it.</summary>
    internal Border Enlarged { get; } = new() { Background = new SolidColorBrush(Color.FromArgb(235, 8, 10, 13)), IsVisible = false, Padding = new Thickness(24) };

    /// <summary>Tests: what opens a web address (the system's browser).</summary>
    internal Func<Uri, Task<bool>>? OpenUrl { get; set; }

    /// <summary>The places shown, oldest first, and which one is shown (Back and Forward move along them).</summary>
    private readonly List<HelpTarget> _visited = [];

    /// <summary>Which of the places visited is shown.</summary>
    private int _at = -1;

    /// <summary>The page shown, read.</summary>
    private Markdig.Syntax.MarkdownDocument? _doc;

    /// <summary>The page's title, above it.</summary>
    private readonly TextBlock _title = new() { FontSize = 13, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

    /// <summary>Builds the window, on the first page.</summary>
    public HelpWindow()
    {
        Title = Lang.T("docs.title");
        Width = 1180;
        Height = 820;
        MinWidth = 640;
        MinHeight = 420;
        Background = Ui.Bg;
        Icon = MainWindow.AppIcon();
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        BackButton.Click += (_, _) => Move(-1);
        ForwardButton.Click += (_, _) => Move(1);
        HomeButton.Click += (_, _) => Go(HelpTarget.Home);
        BrowserButton.Click += async (_, _) => await (OpenUrl ?? Launcher.LaunchUriAsync)(HelpDocs.OnGitHub(Shown));
        SearchBox.Tip("docs.search");
        SearchBox.TextChanged += (_, _) => ShowSide();
        SearchBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) { OpenFirstHit(); } };
        Enlarged.PointerReleased += (_, _) => Enlarged.IsVisible = false;
        KeyDown += OnKey;
        Content = Layout();
        Closed += (_, _) => Current = null;
        Go(HelpTarget.Home);
    }

    /// <summary>
    /// Shows a place of the documentation in the Help window: in the one open (brought to the front), or a new one.
    /// </summary>
    /// <param name="target">The place; the first page when null.</param>
    /// <returns>The window.</returns>
    public static HelpWindow Open(HelpTarget? target = null)
    {
        var w = Current ?? new HelpWindow();
        Current = w;
        w.Go(target ?? HelpTarget.Home);
        w.Show();
        w.Activate();
        return w;
    }

    /// <summary>A button of the bar above the page: an icon, its tip.</summary>
    private static Button Bar(string icon, string tip) =>
        new Button { Content = Icons.Make(icon, 17), Padding = new Thickness(7, 5) }.Classed("ghost").Tip(tip);

    /// <summary>The window's parts: the side (search, contents) and the page with its bar; the enlarged picture over all.</summary>
    private Grid Layout()
    {
        var side = new DockPanel { Background = Ui.Panel2 };
        DockPanel.SetDock(SearchBox, Dock.Top);
        side.Children.Add(SearchBox);
        side.Children.Add(new ScrollViewer { Content = Side });
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"), ColumnSpacing = 2, Margin = new Thickness(10, 8) };
        bar.Children.Add(BackButton);
        bar.Children.Add(Ui.Col(ForwardButton, 1));
        bar.Children.Add(Ui.Col(HomeButton, 2));
        bar.Children.Add(Ui.Col(_title, 3));
        _title.Margin = new Thickness(10, 0);
        bar.Children.Add(Ui.Col(BrowserButton, 4));
        var main = new DockPanel();
        var barRow = new Border { Child = bar, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1) };
        DockPanel.SetDock(barRow, Dock.Top);
        main.Children.Add(barRow);
        main.Children.Add(Scroller);
        var all = new Grid { ColumnDefinitions = new ColumnDefinitions("280,*") };
        all.Children.Add(new Border { Child = side, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 1, 0) });
        all.Children.Add(Ui.Col(main, 1));
        Grid.SetColumnSpan(Enlarged, 2);
        all.Children.Add(Enlarged);
        return all;
    }

    /// <summary>Shows a place, as a new step after the one shown (Back comes back to it).</summary>
    /// <param name="target">The place.</param>
    internal void Go(HelpTarget target)
    {
        if (_at >= 0 && _visited[_at] == target)
        {
            Display(target);
            return;
        }
        _visited.RemoveRange(_at + 1, _visited.Count - _at - 1);
        _visited.Add(target);
        _at++;
        Display(target);
    }

    /// <summary>Back (-1) or Forward (1) along the places visited; nothing past either end.</summary>
    internal void Move(int step)
    {
        if (_at + step >= 0 && _at + step < _visited.Count)
        {
            _at += step;
            Display(_visited[_at]);
        }
    }

    /// <summary>
    /// Shows a place: its page drawn (again only when it is another page), then scrolled to its heading (to the top for
    /// none, or for a heading the page does not have).
    /// </summary>
    private void Display(HelpTarget target)
    {
        if (View == null || target.Page != Shown.Page)
        {
            var doc = _doc = HelpDocs.Read(target.Page);
            View = new HelpView(target.Page, Follow, Enlarge);
            var page = View.Build(doc);
            page.MaxWidth = PageWidth;
            page.Margin = new Thickness(36, 24, 36, 48);
            page.HorizontalAlignment = HorizontalAlignment.Center;
            Scroller.Content = page;
            _title.Text = HelpDocs.Title(doc, target.Page);
            Title = target.Page == HelpDocs.Home ? Lang.T("docs.title") : Lang.T("docs.titleOf", _title.Text);
        }
        Shown = target;
        Enlarged.IsVisible = false;
        BackButton.IsEnabled = _at > 0;
        ForwardButton.IsEnabled = _at < _visited.Count - 1;
        ShowSide();
        ScrollTo(target.Anchor);
    }

    /// <summary>Scrolls the page to a heading once it is laid out (to the top for none, or one it does not have).</summary>
    private void ScrollTo(string? anchor)
    {
        Scroller.Offset = default;
        if (anchor == null || View?.Anchors.GetValueOrDefault(anchor) is not { } heading)
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (Scroller.Content is Visual page && heading.TranslatePoint(default, page) is { } at)
            {
                Scroller.Offset = new Vector(0, Math.Max(0, at.Y + page.Bounds.Top - 12));
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Follows a link of the page: a place in the documentation is shown here, anything else in the browser.</summary>
    /// <param name="href">The link as written.</param>
    internal void Follow(string href)
    {
        switch (HelpDocs.Resolve(Shown.Page, href))
        {
            case { Target: { } target }:
                Go(target);
                break;
            case { Web: { } uri }:
                _ = (OpenUrl ?? Launcher.LaunchUriAsync)(uri);
                break;
        }
    }

    /// <summary>Shows a picture of the documentation full size over the page.</summary>
    /// <param name="path">Its path under docs/.</param>
    internal void Enlarge(string path)
    {
        using var stream = HelpDocs.Open(path);
        if (stream == null)
        {
            return;
        }
        Enlarged.Child = new Image { Source = new Avalonia.Media.Imaging.Bitmap(stream), Stretch = Stretch.Uniform };
        Enlarged.IsVisible = true;
    }

    /// <summary>
    /// The window's keys: Alt + Left and Right go back and forward, Ctrl + F searches, Esc closes the enlarged picture
    /// or else empties the search.
    /// </summary>
    private void OnKey(object? sender, KeyEventArgs e)
    {
        bool alt = e.KeyModifiers == KeyModifiers.Alt;
        switch (e.Key)
        {
            case Key.Left when alt:
                Move(-1);
                break;
            case Key.Right when alt:
                Move(1);
                break;
            case Key.F when e.KeyModifiers == KeyModifiers.Control:
                SearchBox.Focus();
                SearchBox.SelectAll();
                break;
            case Key.Escape when Enlarged.IsVisible:
                Enlarged.IsVisible = false;
                break;
            case Key.Escape:
                SearchBox.Text = "";
                break;
            default:
                return;
        }
        e.Handled = true;
    }
}
