using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Terrain;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>Builds the panel (hidden), on the Pieces tab.</summary>
    /// <param name="input">The Place tool's input: its tool puts the pieces down, its turn and lift steps are set here.</param>
    /// <param name="bar">The building bar the dock builds with (the window's, shared with Select and Paste); null: one of its own.</param>
    public BuildPanel(PlaceInput input, BuildingBar? bar = null)
    {
        _input = input;
        Bar = bar ?? new BuildingBar();
        foreach (int cat in Tabs.Where(c => c != Mods))
        {
            string name = TabName(cat);
            var b = new Button { Content = name, FontSize = 11, Padding = new Thickness(8, 2), Tag = cat };
            ToolTip.SetTip(b, cat switch
            {
                Plants => Lang.T("build.tab.plants"),
                Feasts => Lang.T("build.tab.feasts"),
                _ => Lang.T("build.tab.hammer", name),
            });
            b.Click += (_, _) => { _tab = cat; Search.Text = ""; Render(); };
            HammerTabs.Children.Add(b);
        }
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { Render(); } };
        Tool.Changed += ShowSnapPoint;
        Tool.Changed += ShowLift;
        Bar.Changed += TakeBar;
        Bar.LiftResetAsked += () =>
        {
            if (Tool.Building)
            {
                _input.Lift(-Tool.HeightNudge);
            }
        };
        Search.Tip("build.search");
        WireLay();
        ShowLift();
        PiecesTab.Click += (_, _) => ShowPieces(mods: false);
        ModsTab.Click += (_, _) => ShowPieces(mods: true);
        LibraryTab.Click += (_, _) => ShowLibrary(true);
        PiecesTab.Tip("build.piecesTab");
        ModsTab.Tip("build.modsTab");
        LibraryTab.Tip("build.libraryTab");
        CollapseButton.Tip("build.collapse");
        CollapseButton.Click += (_, _) => Fold(!Folded);
        Library.Models = () => Models();
        LibrarySearch.Width = 240;
        LibrarySearch.VerticalAlignment = VerticalAlignment.Center;
        GenerateButton.Tip("build.generate");
        GenerateButton.Click += (_, _) => ShowDungeon();
        BackToLibrary.Tip("build.backToLibrary");
        BackToLibrary.Click += (_, _) => ShowLibrary(true);
        _library = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            IsVisible = false,
            Children =
            {
                new ScrollViewer { Content = LibraryList },
                InRow(new TextBlock { Text = Lang.T("build.hint.library"), FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) }, 1),
            },
        };
        var dungeonHead = new DockPanel { Children = { BackToLibrary, new TextBlock { Text = Lang.T("build.head.generate"), FontSize = 13, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) } } };
        DockPanel.SetDock(BackToLibrary, Dock.Left);
        _dungeon = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 6,
            IsVisible = false,
            Children = { dungeonHead, InRow(new ScrollViewer { Content = Dungeon.View }, 1), InRow(Dungeon.Actions, 2) },
        };
        _pieces = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 10,
            Children =
            {
                ModColumn(),
                Ui.Col(new Grid { Children = { NoMods, new ScrollViewer { Content = Tiles } } }, 1),
            },
        };
        _body = new Grid { Height = DefaultHeight, Margin = new Thickness(0, 8, 0, 0), Children = { _pieces, _library, _dungeon } };
        _footer = Footer();
        Card = Ui.Card(new DockPanel
        {
            Children = { Grip(), Header(), _footer, _body },
        });
        Card.Margin = new Thickness(10, 0, 10, 58);
        Card.VerticalAlignment = VerticalAlignment.Bottom;
        Card.IsVisible = false;
        // The building bar comes and goes with the dock (the window shows it again for Select and Paste).
        Card.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty)
            {
                Bar.Show(Card.IsVisible ? BuildingBar.Uses.Build : BuildingBar.Uses.None, BuilderOffered());
                ShowLift();
            }
        };
        Fold(false);
        ShowLibrary(false);
    }

    /// <summary>Whether the building bar offers Built by: in a world (the window says), not on the Workshop's plot.</summary>
    internal Func<bool> BuilderOffered { get; set; } = () => false;

    /// <summary>Shows the lift PgUp, PgDn and Ctrl + wheel give, while the dock has the building bar.</summary>
    public void ShowLift()
    {
        if (Bar.Use is BuildingBar.Uses.Build or BuildingBar.Uses.None)
        {
            Bar.ShowLift(Tool.HeightNudge);
        }
    }

    /// <summary>
    /// Building starts (the Build tool in a world, the Workshop, Build here in a dungeon): the Place tool set for
    /// building (one piece at a time, as it is, facing the turn, or along the line last laid), woodwall (else the first
    /// piece) chosen when no single piece of the list is; none when the list is empty.
    /// </summary>
    /// <param name="library">
    /// The Library tab offered (the Workshop's plot); false in a world and building in a dungeon, where its blueprints
    /// cannot be added: the pieces only.
    /// </param>
    /// <param name="lines">
    /// Lay offered (pieces along a line, a circle or a rectangle drawn on the ground); false building in a dungeon,
    /// whose rooms are far above the ground a line is drawn on.
    /// </param>
    public void Start(bool library = true, bool lines = true)
    {
        LibraryTab.IsVisible = library;
        LinesOffered = lines;
        if (!library)
        {
            ShowLibrary(false);
        }
        var t = Tool;
        t.Mode = PlaceTool.Modes.Brush;
        t.Building = true;
        t.OneAtATime = t.OneAtATimeByHand = true;
        t.RandomYaw = false;
        t.Tilt = 0;
        t.SizeMin = t.SizeMax = 100;
        t.Elevation = PlaceTool.Elevations.Ground;
        t.SnapMode = SnapMode;
        t.SnapTo = Bar.Snaps;
        t.SnapPoint = 0;
        t.Fine = false;
        t.OnTop = false;
        t.GridStep = Bar.GridStep;
        _input.TurnStep = Bar.TurnStep;
        _input.LiftStep = Bar.LiftStep;
        TakeLay();
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
        RenderRecent();
    }

    /// <summary>Makes a piece the one the Place tool puts down, and shows its name and cost.</summary>
    /// <param name="prefab">The piece's prefab name.</param>
    public void Choose(string prefab)
    {
        Tool.Chosen.Clear();
        Tool.Chosen.Add(prefab);
        // Another piece: its own snap points, counted from its first.
        Tool.SnapPoint = 0;
        Tool.Notify();
        string mod = ModGroup(prefab);
        Chosen.Text = PieceCost.PieceName(prefab) + (mod.Length > 0 ? " · " + mod : "");
        Cost.Text = MaterialsPanel.NoRecipe(prefab) ? Lang.T("cost.unknown") : Lang.T("build.costs", PieceCost.Of([prefab]).Describe(6));
        foreach (var b in Tiles.Children.OfType<Button>().Concat(RecentRow.Children.OfType<Button>()))
        {
            b.Classes.Set("on", b.Tag as string == prefab);
        }
    }
}
