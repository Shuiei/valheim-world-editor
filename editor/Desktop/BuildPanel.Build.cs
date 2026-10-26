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
                    Children = { Ui.Title(Lang.T("rail.label.build"), HelpTopics.Workshop), Ui.Col(PiecesTab, 1), Ui.Col(LibraryTab, 2) },
                },
                _pieces,
                _library,
                _dungeon,
            },
        });
        Card.IsVisible = false;
        ShowLibrary(false);
    }

    /// <summary>Shows the lift Ctrl + wheel gives.</summary>
    public void ShowLift()
    {
        LiftText.Text = Tool.HeightNudge == 0 ? Lang.T("build.liftNone") : $"{Tool.HeightNudge:+0.0#;-0.0#} m";
        LiftReset.IsEnabled = Tool.HeightNudge != 0;
    }

    /// <summary>
    /// Into the Workshop: the Place tool set for building (one piece at a time, as it is, facing the
    /// turn), woodwall (else the first piece) chosen when no single piece of the list is; none when the list is empty.
    /// </summary>
    /// <param name="library">
    /// The Library tab offered (the Workshop's plot); false building in a dungeon, where its blueprints cannot be added:
    /// the pieces only.
    /// </param>
    public void Start(bool library = true)
    {
        LibraryTab.IsVisible = library;
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
}
