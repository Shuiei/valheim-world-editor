using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The editor's window: its pages (the start page, a new world, the world map, and the 3D editor of an area or the
/// Workshop), the top and save bars, the tool panels, and the help card with the frame rate, what is loaded, and the
/// Record frame rates switch.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// The help card's frame rate.
    /// </summary>
    private readonly TextBlock _fps = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// The help card's notes: how the area was loaded and the 3D view's status lines, newest first.
    /// </summary>
    internal TextBlock LoadedInfo { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// Writes frame rates to a file while Record frame rates is on.
    /// </summary>
    private readonly PerfLog _perf = new();
    /// <summary>
    /// Walking or flying: how to move, in the status bar; empty otherwise.
    /// </summary>
    private readonly TextBlock _eye = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(143, 240, 180)), TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// Help (?, F3): the controls, the graphics card, what was loaded, the frame rate.
    /// </summary>
    internal Border HelpCard { get; private set; } = null!;
    /// <summary>
    /// Shows or hides the help card.
    /// </summary>
    internal Button HelpButton { get; } = new Button { Content = Icons.Make("help", 18), Padding = new Thickness(7, 5) }.Classed("ghost");
    /// <summary>
    /// Opens the View panel.
    /// </summary>
    internal Button ViewButton { get; } = new Button { Content = Icons.With("view", Lang.T("rail.label.view")), FontSize = 12.5 }.Classed("ghost");
    /// <summary>
    /// The top bar's names: the world, and the area open.
    /// </summary>
    internal TextBlock TitleText { get; } = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, Text = "Valheim World Editor", TextTrimming = TextTrimming.CharacterEllipsis };
    /// <summary>
    /// Under the title: the area open, or in the Workshop the building's cost.
    /// </summary>
    internal TextBlock SubtitleText { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextTrimming = TextTrimming.CharacterEllipsis };
    /// <summary>
    /// The LIVE badge, shown while the world is the game's live one.
    /// </summary>
    private readonly Border _liveBadge = new()
    {
        IsVisible = false,
        CornerRadius = new CornerRadius(999),
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Color.Parse("#2a6a4a")),
        Background = new SolidColorBrush(Color.Parse("#13261d")),
        Padding = new Thickness(9, 4),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.Parse("#4be08a")), VerticalAlignment = VerticalAlignment.Center, BoxShadow = BoxShadows.Parse("0 0 6 0 #4be08a") },
                new TextBlock { Text = Lang.T("top.live"), FontSize = 11.5, FontWeight = FontWeight.Bold, LetterSpacing = 0.7, Foreground = new SolidColorBrush(Color.Parse("#8ff0b4")) },
            },
        },
    };
    /// <summary>
    /// The save bar's pill around what is waiting, tinted while something is.
    /// </summary>
    private readonly Border _pendingPill = new() { CornerRadius = new CornerRadius(999), BorderThickness = new Thickness(1), Padding = new Thickness(9, 4), VerticalAlignment = VerticalAlignment.Center };
    /// <summary>
    /// What is selected, in the status bar.
    /// </summary>
    private readonly TextBlock _selection = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(224, 166, 75)), TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// The game's models once opened; null before, or without the game.
    /// </summary>
    private ModelStore? _models;
    /// <summary>
    /// The game's models being opened at start (the bundles indexed the first time: a few seconds).
    /// </summary>
    private Task<ModelStore?>? _modelsLoading;
    /// <summary>
    /// The file pickers' patterns.
    /// </summary>
    private static readonly string[] BlueprintPatterns = ["*.blueprint", "*.vbuild"], PicturePatterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"], PngPatterns = ["*.png"];
    /// <summary>
    /// A thing's name for people: its model's, else its prefab's, else the prefab hash.
    /// </summary>
    private string ThingName(WorldScene.Thing t) => _models?.NameOf(t.Prefab) ?? Terrain.PrefabCatalog.DisplayName(t.Prefab) ?? t.Prefab.ToString();

    /// <summary>
    /// The 3D view.
    /// </summary>
    public GlView View { get; } = new();
    /// <summary>
    /// The tool rail and the tool options.
    /// </summary>
    internal ToolPanel Tools { get; } = new();
    /// <summary>
    /// The Select tool's panel.
    /// </summary>
    internal SelectPanel SelectPanel { get; }
    /// <summary>
    /// The Measure tool's panel.
    /// </summary>
    internal MeasurePanel MeasurePanel { get; }
    /// <summary>
    /// The Dungeon tool's panel.
    /// </summary>
    internal DungeonPanel DungeonPanel { get; }
    /// <summary>
    /// The Shape tool's panel.
    /// </summary>
    internal ShapePanel ShapePanel { get; } = new();
    /// <summary>
    /// The Mountain tool's panel.
    /// </summary>
    internal MountainPanel MountainPanel { get; } = new();
    /// <summary>
    /// The Workshop's Build panel (made once the models are there).
    /// </summary>
    internal BuildPanel BuildPanel { get; private set; } = null!;
    /// <summary>
    /// The Script tool's panel.
    /// </summary>
    internal ScriptPanel ScriptPanel { get; } = new();
    /// <summary>
    /// Stops the running script (null: none runs).
    /// </summary>
    private Action? _stopScript;
    /// <summary>
    /// The Mask that limits brushes, paths, shapes, area actions and pastes.
    /// </summary>
    internal Mask Mask { get; } = new();
    /// <summary>
    /// The Mask's panel.
    /// </summary>
    internal MaskPanel MaskPanel { get; }
    /// <summary>
    /// The Path tool's panel.
    /// </summary>
    internal PathPanel PathPanel { get; }
    /// <summary>
    /// The Area tool's panel.
    /// </summary>
    internal AreaPanel AreaPanel { get; }
    /// <summary>
    /// The Paste tool's panel.
    /// </summary>
    internal PastePanel PastePanel { get; }
    /// <summary>
    /// The Place tool's settings and work.
    /// </summary>
    internal PlaceTool PlaceTool { get; } = new();
    /// <summary>
    /// The Place tool's mouse and keys over the 3D view.
    /// </summary>
    internal PlaceInput PlaceInput { get; }
    /// <summary>
    /// The Place tool's panel.
    /// </summary>
    internal PlacePanel PlacePanel { get; }

    // The save bar (top middle): what is waiting to be saved, undo, redo, Save, and the last message.
    /// <summary>
    /// The status bar's message (the driver clears it before a documentation picture).
    /// </summary>
    internal string StatusMessage { get => MessageText.Text ?? ""; set => MessageText.Text = value; }
    /// <summary>
    /// Takes the last edit back.
    /// </summary>
    internal Button UndoButton { get; } = new Button { Content = Icons.Make("undo", 18), IsEnabled = false, Padding = new Thickness(7, 5) }.Classed("ghost");
    /// <summary>
    /// Does the edit taken back again.
    /// </summary>
    internal Button RedoButton { get; } = new Button { Content = Icons.Make("redo", 18), IsEnabled = false, Padding = new Thickness(7, 5) }.Classed("ghost");
    /// <summary>
    /// Save to world, Apply live, or in the Workshop Save blueprint.
    /// </summary>
    internal Button SaveButton { get; } = new Button { Content = Lang.T("save.label.save"), FontSize = 12.5, IsEnabled = false }.Classed("primary");
    /// <summary>
    /// Opens the History panel.
    /// </summary>
    internal Button HistoryButton { get; } = new Button { Content = Icons.With("history", Lang.T("history.title")), FontSize = 12.5 }.Classed("ghost");
    /// <summary>
    /// The History panel: the edits so far, to go back to one.
    /// </summary>
    internal HistoryPanel History { get; }
    /// <summary>
    /// Back to the map (in the Workshop: to the start page).
    /// </summary>
    internal Button MapButton { get; } = new Button { Content = Icons.With("back", Lang.T("top.label.map")), FontSize = 12.5, IsVisible = false }.Classed("ghost");

    // ---- Pages: the start page, the world map, and the 3D editor of an area.
    /// <summary>
    /// Where the shown page goes.
    /// </summary>
    private readonly ContentControl _pages = new();
    /// <summary>
    /// The 3D editor's page.
    /// </summary>
    private readonly Control _editorPage = null!;
    /// <summary>
    /// The busy veil over the window while something long runs.
    /// </summary>
    private readonly Border _busy = new() { Background = new SolidColorBrush(Color.FromArgb(200, 10, 12, 16)), IsVisible = false };
    /// <summary>
    /// What the busy veil says is being done.
    /// </summary>
    private readonly TextBlock _busyText = new() { FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    /// <summary>
    /// The world open, or null on the start page or in the Workshop.
    /// </summary>
    internal WorldSession? World { get; private set; }

    /// <summary>
    /// The start page, kept once made (until an error is shown on a new one).
    /// </summary>
    internal StartPage? StartPage { get; private set; }
    /// <summary>
    /// The world map page, kept once made.
    /// </summary>
    internal MapPage? MapPage { get; private set; }
    /// <summary>
    /// Whether the world map is the page shown.
    /// </summary>
    internal bool MapShown => MapPage != null && _pages.Content == MapPage.View;
    /// <summary>
    /// The transparent control over the 3D view that takes the mouse (the test driver sends it input).
    /// </summary>
    internal Control Surface { get; private set; } = null!;

    /// <summary>
    /// What is waiting to be saved, as a list for people ("3 zone(s) of ground, 2 deleted").
    /// </summary>
    private static string Describe((int Zones, int Deleted, int Added, int Resets) p) =>
        PendingList(p.Zones > 0 ? Lang.T("save.count.zones", p.Zones) : "", p.Deleted, p.Added, p.Resets);
}
