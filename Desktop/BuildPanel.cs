using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Workshop's Build panel: the hammer's pieces in its tabs (Building, Heavy building, Furniture…),
/// each with a small picture, searchable; the one clicked is what the Place tool puts down, one at a
/// time under the cursor. Snapping to the pieces already there, stacking on the one under the cursor,
/// a grid for the rest, and the step , and . turn by. What the piece costs in game. Its Library: the
/// blueprints, and Generate dungeon (a dungeon of building pieces made onto the plot).
/// </summary>
public sealed class BuildPanel
{
    /// <summary>The panel itself, hidden until the Workshop opens.</summary>
    public Control Card { get; }
    /// <summary>Searches all pieces by English name or prefab (every word must match), across the tabs.</summary>
    internal TextBox Search { get; } = new() { PlaceholderText = Lang.T("build.hint.search"), FontSize = 12 };
    /// <summary>The piece tiles of the tab or search, with a heading per station.</summary>
    internal WrapPanel Tiles { get; } = new() { ItemSpacing = 4, LineSpacing = 4 };
    /// <summary>Snap to the snap points of the pieces already there.</summary>
    internal CheckBox SnapBox { get; } = new CheckBox { Content = Lang.T("build.label.snap"), FontSize = 12, IsChecked = true }.Classed("switch");
    /// <summary>The grid step for pieces not snapped (Off, 0.5, 1 or 2 m).</summary>
    internal ComboBox GridBox { get; } = new() { ItemsSource = new[] { Lang.T("build.grid.off"), "0.5 m", "1 m", "2 m" }, SelectedIndex = 0, FontSize = 12 };
    /// <summary>The step , and . turn the piece by. The game turns the hammer's piece by 22.5° a step.</summary>
    internal ComboBox TurnBox { get; } = new() { ItemsSource = new[] { "1°", "15°", "22.5°", "45°", "90°" }, SelectedIndex = 2, FontSize = 12 };
    /// <summary>The chosen piece's English name.</summary>
    internal TextBlock Chosen { get; } = new() { FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    /// <summary>What the chosen piece costs in game.</summary>
    internal TextBlock Cost { get; } = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(214, 190, 140)), TextWrapping = TextWrapping.Wrap };
    /// <summary>The lift Ctrl + wheel gives (see <see cref="ShowLift"/>).</summary>
    internal TextBlock LiftText { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>Takes the lift back to none.</summary>
    internal Button LiftReset { get; } = new() { Content = Lang.T("build.label.reset"), FontSize = 11, Padding = new Thickness(6, 1) };
    /// <summary>The game's models, for the pictures (null: each piece's boxes).</summary>
    internal Func<ModelStore?> Models { get; set; } = () => null;

    /// <summary>
    /// The tab numbers of the cultivator's pieces (plants), the serving tray's (feasts) and the mods' (whatever their
    /// tool and category): not hammer categories.
    /// </summary>
    internal const int Plants = 100, Feasts = 101, Mods = 102;
    /// <summary>
    /// The hammer's tabs (Piece.PieceCategory), in its order, then the cultivator's and the serving
    /// tray's pieces (plants, feasts), then the mods' (shown when the game has some). Named by <see cref="TabName"/>.
    /// </summary>
    internal static readonly int[] Tabs = [2, 3, 4, 1, 0, 5, Plants, Feasts, Mods];

    /// <summary>A tab's name, in the language in use.</summary>
    /// <param name="category">The tab (one of <see cref="Tabs"/>).</param>
    internal static string TabName(int category) => category switch
    {
        2 => Lang.T("build.tabs.building"),
        3 => Lang.T("build.tabs.heavyBuilding"),
        4 => Lang.T("build.tabs.furniture"),
        1 => Lang.T("build.tabs.crafting"),
        0 => Lang.T("build.tabs.misc"),
        5 => Lang.T("build.tabs.more"),
        Plants => Lang.T("build.tabs.plants"),
        Feasts => Lang.T("build.tabs.feasts"),
        Mods => Lang.T("build.tabs.mods"),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Not one of the Build panel's tabs."),
    };

    /// <summary>The grid steps behind <see cref="GridBox"/>'s entries, in metres (0: off).</summary>
    private static readonly float[] Grids = [0, 0.5f, 1, 2];
    /// <summary>The turn steps behind <see cref="TurnBox"/>'s entries, in degrees.</summary>
    private static readonly float[] Turns = [1, 15, 22.5f, 45, 90];

    /// <summary>The Place tool's input, whose tool puts the pieces down.</summary>
    private readonly PlaceInput _input;
    /// <summary>The tab buttons.</summary>
    private readonly WrapPanel _tabs = new() { ItemSpacing = 3, LineSpacing = 3 };
    /// <summary>On the Mods tab, one button per mod: the tab lists one mod's pieces at a time.</summary>
    private readonly WrapPanel _modRow = new() { ItemSpacing = 3, LineSpacing = 3, IsVisible = false };
    /// <summary>The mod whose pieces the Mods tab lists (a <see cref="ModGroup"/>).</summary>
    private string? _mod;
    /// <summary>The tiles' pictures still being drawn, by prefab: set when drawn.</summary>
    private readonly Dictionary<string, List<Image>> _waiting = [];
    /// <summary>At most this many tiles are listed at once (a mod can add thousands of pieces): the search narrows.</summary>
    internal const int MaxTiles = 300;
    /// <summary>The tab shown (a category of <see cref="Tabs"/>).</summary>
    private int _tab = 2;
    /// <summary>The pieces' pictures by prefab, once drawn.</summary>
    private readonly Dictionary<string, Bitmap?> _pictures = [];
    /// <summary>The prefabs whose pictures are being drawn (or were), so each is drawn once.</summary>
    private readonly HashSet<string> _drawing = [];

    /// <summary>
    /// Everything players build: the hammer's pieces, the cultivator's, the serving tray's and the mods' (prefab,
    /// English name, tab). The hoe's are ground edits, not pieces. Ordered by station, then by name. Worked out again
    /// when the game's data changes (mods' pieces learned from a live game) or the mods' manifests do (pieces not
    /// offered, groups).
    /// </summary>
    internal static List<(string Prefab, string Name, int Category)> Pieces
    {
        get
        {
            var made = _pieceList;
            var now = (GameData.Generation, ModManifests.Generation);
            if (made.Generation != now)
            {
                made = (now, ListPieces());
                _pieceList = made;
            }
            return made.List;
        }
    }

    /// <summary>What <see cref="Pieces"/> last worked out, and from which data.</summary>
    private static ((int Data, int Manifests) Generation, List<(string Prefab, string Name, int Category)> List) _pieceList = ((-1, -1), []);

    /// <summary>Works out <see cref="Pieces"/>.</summary>
    private static List<(string Prefab, string Name, int Category)> ListPieces() =>
        [.. PieceCatalog.Names.Select(n => (n, PieceCatalog.Get(StableHash.Of(n))))
            .Where(p => p.Item2 is { IsBuildPiece: true } && !ModManifests.IsHidden(p.n))
            .Select(p => (p.n, PieceCost.PieceName(p.n), ModCatalogue.IsModded(p.n) ? Mods : p.Item2!.Tool switch { "cultivator" => Plants, "feaster" => Feasts, _ => p.Item2.Category }))
            .OrderBy(p => ModGroup(p.n), StringComparer.OrdinalIgnoreCase).ThenBy(p => PieceGroup(p.n) ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(p => StationRank(PieceCost.Get(p.n)?.Station)).ThenBy(p => PieceCost.Get(p.n)?.Station ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Item2, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The mod a piece of the Mods tab is listed under ("Other mods" when the game did not say); empty for the game's
    /// own pieces. A mod that adds many (PlanBuild's plans of every piece) does not bury the others.
    /// </summary>
    internal static string ModGroup(string prefab) => ModCatalogue.IsModded(prefab) ? ModCatalogue.ModOf(prefab) ?? Lang.T("build.otherMods") : "";

    /// <summary>
    /// The group a mod's manifest puts a mod piece in (the Mods tab's heading in place of its station); null for the
    /// game's own pieces and when the manifest says none.
    /// </summary>
    internal static string? PieceGroup(string prefab) => ModCatalogue.IsModded(prefab) ? ModManifests.PrefabOf(prefab)?.Group : null;

    /// <summary>
    /// A station's rank, to list pieces by the station they need, in the order players get them (none first:
    /// built anywhere; one not listed last), then by name.
    /// </summary>
    internal static int StationRank(string? station) => station?.ToLowerInvariant() switch
    {
        null or "" => 0,
        "workbench" => 1,
        "stonecutter" => 2,
        "forge" => 3,
        "artisan table" => 4,
        "black forge" => 5,
        "galdr table" => 6,
        _ => 7,
    };

    /// <summary>Builds the panel (hidden), on the Pieces tab.</summary>
    /// <param name="input">The Place tool's input: its tool puts the pieces down, its turn step is set here.</param>
    public BuildPanel(PlaceInput input)
    {
        _input = input;
        foreach (int cat in Tabs)
        {
            string name = TabName(cat);
            var b = new Button { Content = name, FontSize = 11, Padding = new Thickness(7, 2), Tag = cat };
            ToolTip.SetTip(b, cat switch
            {
                Plants => Lang.T("build.tab.plants"),
                Feasts => Lang.T("build.tab.feasts"),
                Mods => Lang.T("build.tab.mods"),
                _ => Lang.T("build.tab.hammer", name),
            });
            b.Click += (_, _) => { _tab = cat; Search.Text = ""; Render(); };
            _tabs.Children.Add(b);
        }
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { Render(); } };
        SnapBox.IsCheckedChanged += (_, _) => { Tool.SnapTo = SnapBox.IsChecked == true; Tool.Notify(); };
        GridBox.SelectionChanged += (_, _) => { Tool.GridStep = Grids[Math.Max(0, GridBox.SelectedIndex)]; Tool.Notify(); };
        TurnBox.SelectionChanged += (_, _) => _input.TurnStep = Turns[Math.Max(0, TurnBox.SelectedIndex)];
        SnapBox.Tip("build.snap");
        GridBox.Tip("build.grid");
        TurnBox.Tip("build.turn");
        Search.Tip("build.search");
        LiftReset.Tip("build.lift");
        LiftReset.Click += (_, _) => { _input.Lift(-Tool.HeightNudge); ShowLift(); };
        ShowLift();
        static Control Row(string label, Control c) => new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("90,*"),
            Children = { new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Ui.Col(c, 1) },
        };
        PiecesTab.Click += (_, _) => ShowLibrary(false);
        LibraryTab.Click += (_, _) => ShowLibrary(true);
        PiecesTab.Tip("build.piecesTab");
        LibraryTab.Tip("build.libraryTab");
        LibrarySearch.Tip("build.librarySearch");
        ImportButton.Tip("blueprints.import");
        ImportButton.Click += (_, _) => ImportAsked?.Invoke();
        GenerateButton.Tip("build.generate");
        GenerateButton.Click += (_, _) => ShowDungeon();
        BackToLibrary.Tip("build.backToLibrary");
        BackToLibrary.Click += (_, _) => ShowLibrary(true);
        LibrarySearch.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { RenderLibrary(again: false); } };
        _library = new StackPanel
        {
            Spacing = 6,
            IsVisible = false,
            Children =
            {
                LibrarySearch,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { ImportButton, GenerateButton } },
                new ScrollViewer { MaxHeight = 530, Content = LibraryList },
                new TextBlock
                {
                    Text = Lang.T("build.hint.library"),
                    FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        _dungeon = new StackPanel
        {
            Spacing = 6,
            IsVisible = false,
            Children =
            {
                new DockPanel { Children = { BackToLibrary, new TextBlock { Text = Lang.T("build.head.generate"), FontSize = 13, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) } } },
                new ScrollViewer { MaxHeight = 560, Content = Dungeon.View },
                Dungeon.Actions,
            },
        };
        DockPanel.SetDock(BackToLibrary, Dock.Left);
        _pieces = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Search,
                _tabs,
                _modRow,
                new ScrollViewer { MaxHeight = 360, Content = Tiles },
                Chosen,
                Cost,
                SnapBox,
                Row(Lang.T("build.label.grid"), GridBox),
                Row(Lang.T("build.label.turnBy"), TurnBox),
                Row(Lang.T("build.label.lift"), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { LiftText, LiftReset } }),
                new TextBlock
                {
                    Text = Lang.T("build.hint.hammer"),
                    FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        Card = Ui.Card(new StackPanel
        {
            Width = 340,
            Spacing = 6,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                    ColumnSpacing = 4,
                    Children = { new TextBlock { Text = Lang.T("rail.label.build"), FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }, Ui.Col(PiecesTab, 1), Ui.Col(LibraryTab, 2) },
                },
                _pieces,
                _library,
                _dungeon,
            },
        });
        Card.IsVisible = false;
        ShowLibrary(false);
    }

    // ---- The Library tab: Homestead's blueprints, opened on the plot, added to it, or dragged onto it.
    /// <summary>Shows the pieces.</summary>
    internal Button PiecesTab { get; } = new() { Content = Lang.T("build.label.pieces"), FontSize = 12, Padding = new Thickness(8, 2) };
    /// <summary>Shows the Library: the blueprints.</summary>
    internal Button LibraryTab { get; } = new() { Content = Lang.T("build.label.library"), FontSize = 12, Padding = new Thickness(8, 2) };
    /// <summary>Searches the blueprints by name, description and tags (every word must match).</summary>
    internal TextBox LibrarySearch { get; } = new() { PlaceholderText = Lang.T("blueprints.hint.search"), FontSize = 12 };
    /// <summary>Imports another mod's blueprint file (the window handles it).</summary>
    internal Button ImportButton { get; } = new() { Content = Lang.T("blueprints.label.import"), FontSize = 12 };
    /// <summary>The blueprint rows.</summary>
    internal StackPanel LibraryList { get; } = new() { Spacing = 4 };
    /// <summary>Generate dungeon: the Dungeon panel's generator, made of building pieces, onto the plot.</summary>
    internal Button GenerateButton { get; } = new() { Content = Lang.T("build.label.generate"), FontSize = 12 };
    /// <summary>From the generator back to the Library.</summary>
    internal Button BackToLibrary { get; } = new() { Content = Lang.T("build.label.backToLibrary"), FontSize = 12 };
    /// <summary>The dungeon generator, in its Workshop form.</summary>
    internal DungeonGenPanel Dungeon { get; } = new(inWorkshop: true);
    /// <summary>The Pieces, Library and generator views, one shown at a time.</summary>
    private readonly StackPanel _pieces, _library, _dungeon;
    /// <summary>The blueprints' folder (Homestead's).</summary>
    internal Func<string> Folder { get; set; } = () => "";
    /// <summary>Open: the blueprint alone on the plot; Add: with what is there (in the middle). Given its file.</summary>
    public event Action<string>? OpenAsked, AddAsked;
    /// <summary>Import file… was clicked.</summary>
    public event Action? ImportAsked;
    /// <summary>What a blueprint dragged from the list carries: its file.</summary>
    internal static readonly DataFormat<string> BlueprintFormat = DataFormat.CreateInProcessFormat<string>("vwe-blueprint");

    /// <summary>Whether the Library is shown.</summary>
    internal bool LibraryShown => _library.IsVisible;
    /// <summary>Whether the dungeon generator is shown.</summary>
    internal bool DungeonShown => _dungeon.IsVisible;

    /// <summary>Shows the Library (true, listed again) or the pieces (false).</summary>
    public void ShowLibrary(bool on)
    {
        _library.IsVisible = on;
        _pieces.IsVisible = !on;
        _dungeon.IsVisible = false;
        PiecesTab.Classes.Set("on", !on);
        LibraryTab.Classes.Set("on", on);
        if (on)
        {
            RenderLibrary();
        }
    }

    /// <summary>The generator, in the Library's place (its tab stays lit).</summary>
    internal void ShowDungeon()
    {
        _library.IsVisible = _pieces.IsVisible = false;
        _dungeon.IsVisible = true;
        PiecesTab.Classes.Set("on", false);
        LibraryTab.Classes.Set("on", true);
    }

    /// <summary>The blueprints last read from the folder, and that folder: searched through again as the search changes.</summary>
    private (string Folder, List<Homestead.Entry> Entries)? _libraryRead;

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
                            new TextBlock { Text = Lang.T("build.libraryRow", e.Pieces, PieceCost.Of(e.Kinds).Describe(3)), FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
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

    /// <summary>The Place tool the panel sets.</summary>
    private PlaceTool Tool => _input.Tool;

    /// <summary>
    /// Into the Workshop: the Place tool set for building (one piece at a time, as it is, facing the
    /// turn), woodwall (else the first piece) chosen when no single piece of the list is; none when the list is empty.
    /// </summary>
    public void Start()
    {
        var t = Tool;
        t.Mode = PlaceTool.Modes.Brush;
        t.Building = true;
        t.OneAtATime = t.OneAtATimeByHand = true;
        t.RandomYaw = false;
        t.Tilt = 0;
        t.SizeMin = t.SizeMax = 100;
        t.Elevation = PlaceTool.Elevations.Ground;
        t.SnapTo = SnapBox.IsChecked == true;
        t.OnTop = false;
        t.GridStep = Grids[Math.Max(0, GridBox.SelectedIndex)];
        _input.TurnStep = Turns[Math.Max(0, TurnBox.SelectedIndex)];
        var pieces = Pieces;
        if (pieces.Count == 0)
        {
            // Nothing to build with (the game's data not read, or every piece hidden by the mods' manifests).
            t.Chosen.Clear();
            Chosen.Text = Lang.T("build.noPieces");
            Cost.Text = "";
        }
        else if (!(t.Chosen.Count == 1 && pieces.Any(p => p.Prefab == t.Chosen[0])))
        {
            Choose(pieces.FirstOrDefault(p => p.Prefab == "woodwall").Prefab ?? pieces[0].Prefab);
        }
        t.Notify();
        Render();
    }

    /// <summary>Shows the lift Ctrl + wheel gives.</summary>
    public void ShowLift()
    {
        LiftText.Text = Tool.HeightNudge == 0 ? Lang.T("build.liftNone") : $"{Tool.HeightNudge:+0.0#;-0.0#} m";
        LiftReset.IsEnabled = Tool.HeightNudge != 0;
    }

    /// <summary>Makes a piece the one the Place tool puts down, and shows its name and cost.</summary>
    /// <param name="prefab">The piece's prefab name.</param>
    public void Choose(string prefab)
    {
        Tool.Chosen.Clear();
        Tool.Chosen.Add(prefab);
        Tool.Notify();
        Chosen.Text = PieceCost.PieceName(prefab);
        Cost.Text = Lang.T("build.costs", PieceCost.Of([prefab]).Describe(6));
        foreach (var b in Tiles.Children.OfType<Button>())
        {
            b.Classes.Set("on", b.Tag as string == prefab);
        }
    }

    /// <summary>
    /// The Mods tab's row of mods (shown when its pieces come from more than one): a button per mod with its number of
    /// pieces, the chosen one on (the first, when none is chosen yet or it has none any more).
    /// </summary>
    /// <param name="list">The Mods tab's pieces; empty: the row hidden (another tab, or a search).</param>
    private void ShowMods(List<(string Prefab, string Name, int Category)> list)
    {
        var mods = list.GroupBy(p => ModGroup(p.Prefab)).Select(g => (Mod: g.Key, Count: g.Count())).ToList();
        _modRow.IsVisible = mods.Count > 1;
        if (!_modRow.IsVisible)
        {
            return;
        }
        if (_mod == null || !mods.Any(m => m.Mod == _mod))
        {
            _mod = mods[0].Mod;
        }
        _modRow.Children.Clear();
        foreach (var (mod, count) in mods)
        {
            var b = new Button { Content = $"{mod} ({count})", FontSize = 11, Padding = new Thickness(7, 2), Tag = mod };
            b.Classes.Set("on", mod == _mod);
            ToolTip.SetTip(b, Lang.T("build.modTip", mod));
            b.Click += (_, _) => { _mod = mod; Render(); };
            _modRow.Children.Add(b);
        }
    }

    /// <summary>Fills the tiles with the tab's pieces, or with those matching the search across all tabs.</summary>
    internal void Render()
    {
        Tiles.Children.Clear();
        string q = Search.Text?.Trim() ?? "";
        var pieces = Pieces;
        foreach (var b in _tabs.Children.OfType<Button>())
        {
            b.Classes.Set("on", q.Length == 0 && (int)b.Tag! == _tab);
            if ((int)b.Tag! == Mods)
            {
                b.IsVisible = pieces.Any(p => p.Category == Mods);
            }
        }
        var list = pieces.Where(p => q.Length > 0
            ? q.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => p.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || p.Prefab.Contains(w, StringComparison.OrdinalIgnoreCase))
            : p.Category == _tab).ToList();
        ShowMods(q.Length == 0 && _tab == Mods ? list : []);
        if (_modRow.IsVisible)
        {
            list = [.. list.Where(p => ModGroup(p.Prefab) == _mod)];
        }
        _waiting.Clear();
        if (list.Count == 0)
        {
            Tiles.Children.Add(new TextBlock { Text = Lang.T("build.noMatch"), FontSize = 11, Foreground = Ui.Muted });
            return;
        }
        int all = list.Count;
        list = [.. list.Take(MaxTiles)];
        string? chosen = Tool.Chosen.Count == 1 ? Tool.Chosen[0] : null;
        string? group = "?";
        foreach (var (prefab, name, _) in list)
        {
            // A heading where the station changes (on the Mods tab, the mod or the station).
            string? station = PieceCost.Get(prefab)?.Station;
            string mod = ModGroup(prefab);
            string heading = (mod.Length > 0 ? mod + " · " : "") + (PieceGroup(prefab) ?? station ?? Lang.T("build.noStation"));
            if (heading != group)
            {
                group = heading;
                Tiles.Children.Add(new TextBlock { Text = heading, Width = 320, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Ui.Muted, Margin = new Thickness(2, 4, 0, 0) });
            }
            var image = new Image { Width = 64, Height = 64, Source = Picture(prefab) };
            if (image.Source == null)
            {
                if (!_waiting.TryGetValue(prefab, out var images))
                {
                    _waiting[prefab] = images = [];
                }
                images.Add(image);
            }
            var b = new Button
            {
                Tag = prefab,
                Width = 76,
                Padding = new Thickness(2),
                Content = new StackPanel
                {
                    Children =
                    {
                        image,
                        new TextBlock { Text = name, FontSize = 10, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis },
                    },
                },
            };
            ToolTip.SetTip(b, $"{name} ({prefab}) · {PieceCost.Of([prefab]).Describe(6)}");
            b.Classes.Set("on", prefab == chosen);
            b.Click += (_, _) => Choose(prefab);
            Tiles.Children.Add(b);
        }
        if (all > list.Count)
        {
            Tiles.Children.Add(new TextBlock { Text = Lang.T("build.showing", list.Count, all), Width = 320, FontSize = 11, Foreground = Ui.Muted, Margin = new Thickness(2, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
        }
    }

    /// <summary>
    /// Pictures of blueprints without one (made elsewhere), drawn from their pieces, by file and its last write time.
    /// Kept in memory only (the folder is left as it is).
    /// </summary>
    private readonly Dictionary<(string, DateTime), Bitmap?> _blueprintPictures = [];

    /// <summary>Pictures being drawn, with the images waiting for each: a blueprint shown again meanwhile is not drawn twice.</summary>
    private readonly Dictionary<(string, DateTime), List<Image>> _blueprintDrawing = [];

    /// <summary>
    /// A blueprint without its picture (made elsewhere): one drawn from its pieces, kept in memory only
    /// (its folder is left as it is), away from the window's thread.
    /// </summary>
    private void DrawPicture(string path, Image image)
    {
        var key = (path, File.GetLastWriteTimeUtc(path));
        if (_blueprintPictures.TryGetValue(key, out var known))
        {
            image.Source = known;
            return;
        }
        if (_blueprintDrawing.TryGetValue(key, out var waiting))
        {
            waiting.Add(image);
            return;
        }
        _blueprintDrawing[key] = [image];
        var models = Models();
        Task.Run(() =>
        {
            try
            {
                var bp = BlueprintFormats.Parse(path, File.ReadAllText(path));
                return BuildingPicture.Draw([.. bp.Pieces.Select(p => new BuildingPicture.Piece(p.Name, p.Position, p.Euler, p.Scale))], models, 128);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            Bitmap? b = null;
            if (t.Result is byte[] png)
            {
                using var ms = new MemoryStream(png);
                b = new Bitmap(ms);
            }
            _blueprintPictures[key] = b;
            if (_blueprintDrawing.Remove(key, out var images))
            {
                foreach (var shown in images)
                {
                    shown.Source = b;
                }
            }
        }), TaskScheduler.Default);
    }

    /// <summary>A piece's picture: drawn once, away from the window's thread, then shown.</summary>
    /// <returns>The picture, or null while it is being drawn (its tiles get it when done).</returns>
    private Bitmap? Picture(string prefab)
    {
        if (_pictures.TryGetValue(prefab, out var known))
        {
            return known;
        }
        if (_drawing.Add(prefab))
        {
            var models = Models();
            Task.Run(() =>
            {
                byte[] png = BuildingPicture.Draw([new BuildingPicture.Piece(prefab, Vector3.Zero, new Vector3(0, 30, 0), 0)], models, 96);
                Dispatcher.UIThread.Post(() =>
                {
                    using var ms = new MemoryStream(png);
                    _pictures[prefab] = new Bitmap(ms);
                    if (_waiting.Remove(prefab, out var images))
                    {
                        foreach (var image in images)
                        {
                            image.Source = _pictures[prefab];
                        }
                    }
                });
            });
        }
        return null;
    }
}
