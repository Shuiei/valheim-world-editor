using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The page's layout: the tools, the legend and the picture's buttons on the left, the drawing (or the world it makes)
/// on the right.
/// </summary>
public sealed partial class DrawWorldPage
{
    /// <summary>The coordinates under the pointer, at the bottom left of the map.</summary>
    internal TextBlock Readout { get; } = new() { FontSize = 11, Margin = new Thickness(10), Foreground = Ui.Muted, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };

    /// <summary>What was opened, saved or went wrong, under the picture's buttons.</summary>
    internal TextBlock NoteText { get; } = new() { FontSize = 11.5, TextWrapping = TextWrapping.Wrap, IsVisible = false };

    /// <summary>The seed the world's small things come from.</summary>
    internal TextBox SeedBox { get; } = new TextBox { PlaceholderText = "Drawn", MaxLength = 10, FontSize = 12 }.Tip("drawWorld.seed");

    /// <summary>The gray of a heightmap at the sea's surface, in percent (black 0, white 100).</summary>
    internal NumericUpDown SeaLevel { get; } = new NumericUpDown { Minimum = 1, Maximum = 99, Value = 10, Increment = 1, FormatString = "0", Width = 110, FontSize = 12 }.Tip("drawWorld.seaLevel");

    internal Button UndoButton { get; } = new Button { Content = Lang.T("drawWorld.label.undo"), FontSize = 12 }.Tip("drawWorld.undo");
    internal Button RedoButton { get; } = new Button { Content = Lang.T("drawWorld.label.redo"), FontSize = 12 }.Tip("drawWorld.redo");

    /// <summary>The tools and the legend: off while the World view shows (nothing to paint on).</summary>
    internal StackPanel ToolsCard { get; } = new() { Spacing = 10 };

    /// <summary>The share of the land each kind has, beside its swatch.</summary>
    private readonly Dictionary<MasterMap.Kind, TextBlock> _shares = [];

    /// <summary>The swatches, to show which kind is chosen.</summary>
    private readonly Dictionary<MasterMap.Kind, ToggleButton> _swatches = [];

    /// <summary>The tools' buttons, to show which is chosen.</summary>
    private readonly Dictionary<MasterMapCanvas.Tool, ToggleButton> _tools = [];

    /// <summary>The world's facts beside the World view: the places the game will lay out, and those it cannot.</summary>
    internal StackPanel Facts { get; } = new() { Spacing = 4 };

    /// <summary>The sign over the map while the world is made.</summary>
    internal Border Loading { get; } = new()
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
        Child = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = Lang.T("drawWorld.msg.making") },
                new ProgressBar { IsIndeterminate = true, Width = 220, Height = 4, MinHeight = 4 },
            },
        },
    };

    /// <summary>A card's heading.</summary>
    private static TextBlock Head(string key) => new() { Text = Lang.T(key), FontSize = 15, FontWeight = FontWeight.SemiBold };

    /// <summary>Lays the page out.</summary>
    private Grid BuildPage()
    {
        var back = new Button { Content = Icons.With("back", Lang.T("workshop.label.start")), FontSize = 12 }.Classed("ghost").Tip("drawWorld.back");
        back.Click += (_, _) => BackRequested?.Invoke();

        // The tools.
        // On a log scale: 10 m (a pixel) to 3 km, each step of the slider the same share bigger.
        var brushSize = new Slider { Minimum = Math.Log10(10), Maximum = Math.Log10(3000), Value = Math.Log10(Canvas.BrushMetres), Width = 190, VerticalAlignment = VerticalAlignment.Center }.Tip("drawWorld.brushSize");
        var brushText = new TextBlock { Text = Lang.T("drawWorld.metres", (int)Canvas.BrushMetres), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Width = 60 };
        brushSize.ValueChanged += (_, e) =>
        {
            Canvas.BrushMetres = (float)Math.Pow(10, e.NewValue);
            brushText.Text = Lang.T("drawWorld.metres", (int)Canvas.BrushMetres);
        };
        var tools = new WrapPanel();
        foreach (var (tool, key) in new[] { (MasterMapCanvas.Tool.Brush, "brush"), (MasterMapCanvas.Tool.Fill, "fill"), (MasterMapCanvas.Tool.Start, "start") })
        {
            var b = new ToggleButton { Content = Lang.T("drawWorld.tool." + key), FontSize = 12, Margin = new Thickness(0, 0, 6, 0), IsChecked = tool == Canvas.Using }.Tip("drawWorld.tool." + key);
            b.Click += (_, _) => Choose(tool);
            _tools[tool] = b;
            tools.Children.Add(b);
        }
        ToolsCard.Children.Add(Head("drawWorld.head.tools"));
        ToolsCard.Children.Add(tools);
        ToolsCard.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = Lang.T("drawWorld.label.brushSize"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, brushSize, brushText } });
        ToolsCard.Children.Add(Ui.Hint(Lang.T("drawWorld.hint.mouse")));

        // The legend: one swatch a kind, the only colours there are.
        ToolsCard.Children.Add(Head("drawWorld.head.legend"));
        var legend = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowSpacing = 4, ColumnSpacing = 6 };
        for (int i = 0; i < MasterMap.Legend.Count; i++)
        {
            var e = MasterMap.Legend[i];
            legend.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var share = new TextBlock { FontSize = 10.5, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center };
            _shares[e.Kind] = share;
            var swatch = new ToggleButton
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 3),
                IsChecked = e.Kind == Canvas.Paint,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), BorderBrush = Ui.Line, BorderThickness = new Thickness(1), Background = new SolidColorBrush(Color.FromRgb(e.R, e.G, e.B)) },
                        new TextBlock { Text = Lang.T(e.Key), FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                        share,
                    },
                },
            };
            ToolTip.SetTip(swatch, Lang.T("tip.drawWorld.kind", Lang.T(e.Key), e.Hex));
            var kind = e.Kind;
            swatch.Click += (_, _) => ChooseKind(kind);
            _swatches[kind] = swatch;
            Grid.SetRow(swatch, i / 2);
            Grid.SetColumn(swatch, i % 2);
            legend.Children.Add(swatch);
        }
        ToolsCard.Children.Add(legend);
        ToolsCard.Children.Add(Ui.Hint(Lang.T("drawWorld.hint.start")));

        // The picture: new, open, save, undo, redo.
        var newButton = new Button { Content = Lang.T("drawWorld.label.new"), FontSize = 12 }.Tip("drawWorld.new");
        var heightmap = new Button { Content = Lang.T("drawWorld.label.heightmap"), FontSize = 12 }.Tip("drawWorld.heightmap");
        heightmap.Click += async (_, _) => await OpenHeightmap();
        var open = new Button { Content = Lang.T("drawWorld.label.open"), FontSize = 12 }.Tip("drawWorld.open");
        var save = new Button { Content = Lang.T("drawWorld.label.save"), FontSize = 12 }.Classed("primary").Tip("drawWorld.save");
        newButton.Click += (_, _) => NewMap();
        open.Click += async (_, _) => await OpenPicture();
        save.Click += async (_, _) => await SavePicture();
        UndoButton.Click += (_, _) => Undo();
        RedoButton.Click += (_, _) => Redo();
        var picture = Ui.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Head("drawWorld.head.picture"),
                new WrapPanel { Children = { newButton, open, save }, ItemSpacing = 6, LineSpacing = 6 },
                new WrapPanel { Children = { UndoButton, RedoButton }, ItemSpacing = 6 },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { heightmap, new TextBlock { Text = Lang.T("drawWorld.label.seaLevel"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, SeaLevel, new TextBlock { Text = "%", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } },
                },
                NoteText,
            },
        });

        // The world: the seed of its small things, and the views.
        var drawing = new ToggleButton { Content = Lang.T("drawWorld.label.drawing"), FontSize = 12, IsChecked = true }.Tip("drawWorld.drawing");
        var world = new ToggleButton { Content = Lang.T("drawWorld.label.world"), FontSize = 12 }.Tip("drawWorld.world");
        drawing.Click += (_, _) => { drawing.IsChecked = true; world.IsChecked = false; ShowDrawing(); };
        world.Click += (_, _) => { world.IsChecked = true; drawing.IsChecked = false; ShowWorld(); };
        var worldCard = Ui.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Head("drawWorld.head.world"),
                new DockPanel { Children = { new TextBlock { Text = Lang.T("common.seed"), Width = 60, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, SeedBox } },
                new WrapPanel { Children = { drawing, world }, ItemSpacing = 6 },
                Ui.Hint(Lang.T("drawWorld.hint.world")),
                Facts,
                new TextBlock { Text = Lang.T("drawWorld.head.checks"), FontSize = 13, FontWeight = FontWeight.SemiBold },
                Checks,
                CreateRows(),
            },
        });

        var column = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Top, Children = { Ui.Card(ToolsCard), picture, worldCard } };
        var left = new ScrollViewer { Content = column, Padding = new Thickness(0, 0, 10, 0) };
        Preview.IsVisible = false;
        var zoom = new StackPanel { Spacing = 4, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        var zoomIn = NewWorldPage.ZoomButton("+", "newWorld.zoomIn");
        var zoomOut = NewWorldPage.ZoomButton("−", "newWorld.zoomOut");
        var fit = NewWorldPage.ZoomButton("⤢", "newWorld.fit");
        zoomIn.Click += (_, _) => { if (Canvas.IsVisible) { Canvas.Zoom(1 / 1.6); } else { Preview.Zoom(1 / 1.6); } };
        zoomOut.Click += (_, _) => { if (Canvas.IsVisible) { Canvas.Zoom(1.6); } else { Preview.Zoom(1.6); } };
        fit.Click += (_, _) => { if (Canvas.IsVisible) { Canvas.Fit(); } else { Preview.Fit(); } };
        zoom.Children.Add(zoomIn);
        zoom.Children.Add(zoomOut);
        zoom.Children.Add(fit);
        var map = new Border
        {
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = new Panel { Children = { Canvas, Preview, Readout, zoom, Loading } },
        };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("400,14,*") };
        body.Children.Add(left);
        Grid.SetColumn(map, 2);
        body.Children.Add(map);
        var page = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(20), RowSpacing = 12 };
        page.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { back, new TextBlock { Text = Lang.T("drawWorld.title"), FontSize = 20, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } });
        Grid.SetRow(body, 1);
        page.Children.Add(body);
        return page;
    }

    /// <summary>Chooses what the left button does.</summary>
    internal void Choose(MasterMapCanvas.Tool tool)
    {
        Canvas.Using = tool;
        foreach (var (t, b) in _tools)
        {
            b.IsChecked = t == tool;
        }
    }

    /// <summary>Chooses the kind the brush and the fill paint (and the brush, unless the fill is chosen).</summary>
    internal void ChooseKind(MasterMap.Kind kind)
    {
        Canvas.Paint = kind;
        foreach (var (k, b) in _swatches)
        {
            b.IsChecked = k == kind;
        }
        if (Canvas.Using == MasterMapCanvas.Tool.Start)
        {
            Choose(MasterMapCanvas.Tool.Brush);
        }
    }

    /// <summary>Each kind's share of the world's land (the water's of the whole), beside its swatch.</summary>
    private void ShowCounts()
    {
        int[] n = Canvas.Map.Counts();
        int land = 0, all = 0;
        for (int k = 0; k < n.Length; k++)
        {
            all += n[k];
            if (!MasterMap.IsWater((MasterMap.Kind)k))
            {
                land += n[k];
            }
        }
        foreach (var e in MasterMap.Legend)
        {
            int of = MasterMap.IsWater(e.Kind) ? all : land;
            int count = n[(int)e.Kind];
            _shares[e.Kind].Text = count == 0 ? "" : of > 0 && count * 1000L / of == 0 ? "<0.1%" : $"{count * 100.0 / of:0.#}%";
        }
        ScheduleChecks();
    }

    /// <summary>
    /// The world's facts beside the World view: the places the game will lay out on it, and those it cannot (a biome
    /// missing, or too small for them).
    /// </summary>
    private void ShowWorldFacts(SeedPreview.Preview p)
    {
        Facts.Children.Clear();
        if (p.Landmarks == null)
        {
            Facts.Children.Add(Ui.Hint(Lang.T("drawWorld.hint.noGameData")));
            return;
        }
        var placed = p.Landmarks.Select(m => m.Name).ToHashSet();
        var missing = SeedPreview.Landmarks.Select(l => l.Name).Distinct().Where(n => !placed.Contains(n)).ToList();
        if (missing.Count == 0)
        {
            Facts.Children.Add(new TextBlock { Text = Lang.T("drawWorld.msg.allPlaced"), FontSize = 12, Foreground = Ui.Ok, TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            Facts.Children.Add(new TextBlock { Text = Lang.T("drawWorld.msg.notPlaced", string.Join(", ", missing.Select(NewWorldPage.LandmarkName))), FontSize = 12, Foreground = Ui.Warn, TextWrapping = TextWrapping.Wrap });
        }
        Facts.Children.Add(Ui.Hint(Lang.T("drawWorld.hint.marks")));
    }
}
