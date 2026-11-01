using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

/// <summary>
/// The panel's frame: a dock across the bottom of the 3D view (the plot keeps the whole window above it), with its tabs
/// and search in a row on top, the pieces (or the blueprints) under it, and the pieces picked last with the chosen one's
/// name and cost at its foot. Its top edge is dragged to make it taller; folded, only its row of tabs is left.
/// </summary>
public sealed partial class BuildPanel
{
    /// <summary>How tall the tiles' part of the dock is at first, in pixels (about two rows of tiles).</summary>
    internal const double DefaultHeight = 236;

    /// <summary>The least and the most the dock's top edge makes the tiles' part, in pixels.</summary>
    internal const double MinHeight = 120, MaxHeight = 640;

    /// <summary>The dock's middle: the pieces, the blueprints or the generator (the part folding hides).</summary>
    private readonly Grid _body;

    /// <summary>The dock's foot: the pieces picked last, and the chosen one's name and cost.</summary>
    private readonly Control _footer;

    /// <summary>Where the dock's height, whether it is folded and the pieces picked last are kept between runs.</summary>
    private Prefs _prefs = Prefs.InMemory();

    /// <summary>The Library's own row in the dock's top row: its search, Import file… and Generate dungeon….</summary>
    private readonly StackPanel _libraryBar = new() { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The hammer's tabs in their scrolling row, with the line before them: shown on the Pieces tab only.</summary>
    private readonly Grid _tabsBar = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Whether the dock is folded (only its row of tabs shown).</summary>
    internal bool Folded { get; private set; }

    /// <summary>Puts a control in a grid row; returns it, for building grids inline.</summary>
    private static Control InRow(Control c, int row)
    {
        Grid.SetRow(c, row);
        return c;
    }

    /// <summary>How tall the dock's tiles' part is, in pixels.</summary>
    internal double BodyHeight => _body.Height;

    /// <summary>
    /// Takes the dock's height, whether it is folded and the pieces picked last from the window's settings, and keeps
    /// them there from now on.
    /// </summary>
    /// <param name="prefs">The window's settings.</param>
    internal void Remember(Prefs prefs)
    {
        _prefs = prefs;
        _body.Height = Math.Clamp(prefs.Get("build.dockHeight", DefaultHeight), MinHeight, MaxHeight);
        _recent.Clear();
        _recent.AddRange(prefs.Get<string[]>("build.recent", []).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal).Take(MaxRecent));
        RenderRecent();
        Fold(prefs.Get("build.dockFolded", false));
    }

    /// <summary>Folds the dock down to its row of tabs (true), or opens it again.</summary>
    internal void Fold(bool folded)
    {
        Folded = folded;
        _body.IsVisible = !folded;
        ShowViewParts(library: _library.IsVisible || _dungeon.IsVisible);
        CollapseButton.Content = folded ? "▴" : "▾";
        CollapseButton.Tip(folded ? "build.expand" : "build.collapse");
        _prefs.Set("build.dockFolded", folded);
    }

    /// <summary>Makes the tiles' part this tall (kept within <see cref="MinHeight"/> and <see cref="MaxHeight"/>), and keeps it.</summary>
    /// <param name="height">The height wanted, in pixels.</param>
    internal void SetBodyHeight(double height)
    {
        _body.Height = Math.Clamp(height, MinHeight, MaxHeight);
        _prefs.Set("build.dockHeight", _body.Height);
    }

    /// <summary>
    /// The dock's top edge: dragged up or down, the tiles' part grows or shrinks (opening the dock when folded).
    /// </summary>
    private Border Grip()
    {
        var grip = new Border
        {
            Height = 8,
            Margin = new Thickness(-Ui.Pad.Left, -Ui.Pad.Top, -Ui.Pad.Right, 2),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            Child = new Border { Width = 44, Height = 3, CornerRadius = new CornerRadius(2), Background = Ui.Line2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        grip.Tip("build.grip");
        DockPanel.SetDock(grip, Dock.Top);
        (double Y, double Height)? from = null;
        grip.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
            {
                from = (e.GetPosition(null).Y, Folded ? MinHeight : _body.Height);
                e.Pointer.Capture(grip);
                e.Handled = true;
            }
        };
        grip.PointerMoved += (_, e) =>
        {
            if (from is not var (y, height))
            {
                return;
            }
            if (Folded)
            {
                Fold(false);
            }
            SetBodyHeight(height + y - e.GetPosition(null).Y);
        };
        grip.PointerReleased += (_, e) =>
        {
            from = null;
            e.Pointer.Capture(null);
        };
        return grip;
    }

    /// <summary>
    /// The dock's top row: its title and help, the Pieces, Mod pieces and Library tabs, the hammer's tabs (on Pieces), and
    /// at the right the search (or the Library's own row) and the fold button.
    /// </summary>
    private Grid Header()
    {
        var title = Ui.Title(Lang.T("rail.label.build"), HelpTopics.Workshop);
        title.ColumnSpacing = 6;
        var views = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Children = { PiecesTab, ModsTab, LibraryTab } };
        _libraryBar.Children.Add(LibrarySearch);
        _libraryBar.Children.Add(ImportButton);
        _libraryBar.Children.Add(GenerateButton);
        var tabs = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = HammerTabs,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { Search, _libraryBar, CollapseButton } };
        _tabsBar.Children.Add(new Border { Width = 1, Height = 22, Background = Ui.Line, VerticalAlignment = VerticalAlignment.Center });
        _tabsBar.Children.Add(Ui.Col(tabs, 1));
        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
            ColumnSpacing = 10,
            Children = { title, Ui.Col(views, 1), Ui.Col(_tabsBar, 2), Ui.Col(right, 3) },
        };
        DockPanel.SetDock(head, Dock.Top);
        return head;
    }

    /// <summary>The dock's foot: the pieces picked last at the left, the chosen piece's name and cost at the right.</summary>
    private Grid Footer()
    {
        var recent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = Lang.T("build.label.recent"), FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center },
                RecentRow,
            },
        };
        var chosen = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Children = { Chosen, Cost } };
        var foot = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10,
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                new ScrollViewer
                {
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    Content = recent,
                },
                Ui.Col(chosen, 1),
            },
        };
        DockPanel.SetDock(foot, Dock.Bottom);
        return foot;
    }

    /// <summary>
    /// The bar of building settings: Snap to pieces, the grid, the turn step and the lift (with its Reset), each with
    /// its label, in one row.
    /// </summary>
    private Border BuildToolbar()
    {
        static Control Labelled(string label, params Control[] controls)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center });
            row.Children.AddRange(controls);
            return row;
        }
        static Border Sep() => new() { Width = 1, Height = 22, Background = Ui.Line, VerticalAlignment = VerticalAlignment.Center };
        var bar = Ui.Card(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                SnapBox,
                Sep(),
                Labelled(Lang.T("build.label.grid"), GridBox),
                Labelled(Lang.T("build.label.turnBy"), TurnBox),
                Sep(),
                Labelled(Lang.T("build.label.lift"), LiftText, LiftReset),
            },
        });
        bar.Padding = new Thickness(12, 6);
        bar.VerticalAlignment = VerticalAlignment.Top;
        bar.IsVisible = false;
        ToolTip.SetTip(bar, Lang.T("build.hint.hammer"));
        return bar;
    }

    /// <summary>
    /// Shows the pieces: the hammer's tabs (false), or the mods' pieces, one mod at a time (true), the search cleared when
    /// the tab changes.
    /// </summary>
    internal void ShowPieces(bool mods)
    {
        bool was = _tab == Mods;
        if (mods)
        {
            _tab = Mods;
        }
        else if (_tab == Mods)
        {
            _tab = 2;
        }
        if (was != mods)
        {
            Search.Text = "";
        }
        ShowLibrary(false);
    }
}
