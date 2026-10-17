using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Draws height lines at the chosen step, or none when they are off.
    /// </summary>
    private void ShowContours() => View.ContourStep = ContourBox.IsChecked == true ? float.Parse((string)ContourStepBox.SelectedItem!) : 0;

    /// <summary>
    /// What is remembered between runs (the tests' windows: in memory only, unless a test gives one).
    /// </summary>
    internal Prefs Prefs { get; }

    /// <summary>
    /// The window, its panels wired to each other; with <paramref name="load"/> it opens on the start page (or, with
    /// --world, straight into that area).
    /// </summary>
    /// <param name="load">False opens the window without a world (tests), its prefs in memory.</param>
    /// <param name="prefs">What is remembered between runs; null for the default.</param>
    public MainWindow(bool load = true, Prefs? prefs = null)
    {
        Prefs = prefs ?? (load ? Prefs.Load() : Prefs.InMemory());
        SelectPanel = new SelectPanel(View.SelectTool);
        History = new HistoryPanel(() => Session);
        Inspector = new InspectorPanel(() => Session);
        Blueprints = new BlueprintsPanel(View.Paste, () => View.Scene);
        WireBlueprints();
        SetUpWorkshop();
        WireInspector();
        WireSelectPanel();
        AskPlayerId = () => Dialogs.AskText(this, Lang.T("select.ask.playerId.title"), Lang.T("select.ask.playerId.text"));
        History.Message += t => { Msg(t); UpdateSaveBar(); };
        History.Allow = AllowEarlier;
        MaskPanel = new MaskPanel(Mask);
        WireGroundTools();
        MeasurePanel = new MeasurePanel(View);
        DungeonPanel = new DungeonPanel(View);
        WireDungeonPanel();
        PathPanel = new PathPanel(View, Tools.Brush);
        PathPanel.ApplyAsked += ApplyPath;
        AreaPanel = new AreaPanel(View, () => Session, prefab => _models?.NameOf(prefab) ?? Terrain.PrefabCatalog.DisplayName(prefab));
        WireAreaPanel();
        PastePanel = new PastePanel(View.Paste);
        SetUpPlaceTool();
        PlaceInput = new PlaceInput(View, PlaceTool) { Session = () => Session };
        PlaceInput.Message += s => { Msg(s); UpdateSaveBar(); };
        View.Place = PlaceInput;
        PlacePanel = new PlacePanel(PlaceInput, () => View.Scene, NameOfPrefab);
        BuildPanel = new BuildPanel(PlaceInput) { Models = () => _models };
        SetUpLibrary();
        WirePlacePanel();
        PastePanel.Done += () => Tools.ChooseMode(ToolMode.Area);
        View.PasteClicked += PasteAt;
        Title = $"Valheim World Editor {BuildInfo.Version}";
        Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri($"avares://{typeof(MainWindow).Assembly.GetName().Name}/Assets/icon.png")));
        Width = Options.WindowWidth > 0 ? Options.WindowWidth : 1500;
        Height = Options.WindowHeight > 0 ? Options.WindowHeight : 950;
        Background = Ui.Bg;
        HelpCard = Help(RecordBox());
        ConfirmSave = text => Dialogs.Ask(this, Lang.T("save.ask.save.title"), text, Lang.T("common.save"));
        Ask = (title, text, yes, no) => Dialogs.Ask(this, title, text, yes, no);
        Tell = text => Dialogs.Tell(this, "Valheim World Editor", text);
        var tools = BuildToolColumn();
        WireShapePanels();
        WireScriptPanel();
        Tools.ToolChanged += OnToolChanged;
        View.SelectTool.Message += (said, _) => Msg(said);
        View.StrokeEnded += OnStrokeEnded;
        WireKeys();
        Closing += async (_, e) => await OnClosingAsync(e);
        // Takes the mouse for the 3D view (see GlView.Attach).
        var surface = new Border { Background = Brushes.Transparent };
        Surface = surface;
        SetUpDrops(surface);
        SetUpEditorWorld();
        ViewPanelCard = ViewPanel();
        LayOutRightPanels();
        _editorPage = new Grid { Children = { View, surface, PlayerLabels, tools, LocationNote, ViewPanelCard, History.Card, Inspector.Card, HelpCard, TopBar(), StatusBar() } };
        _busy.Child = _busyText;
        _pages.Content = _editorPage;
        Content = new Grid { Children = { _pages, _busy } };
        View.Attach(surface, this);
        View.AltWheel = AltWheel;
        View.CtrlWheel = CtrlWheel;
        TrackCursor(surface);
        View.Perf = _perf;
        WireViewReports();
        // Once really closed (Closing also comes when "Keep editing" keeps the window open).
        Closed += (_, _) => { MapPage?.Dispose(); _perf.Flush(); Prefs.Flush(); PlacePanel.Memory.Flush(); };
        RememberPrefs();
        LoadedInfo.Text = Lang.T("world.msg.loading");
        Opened += async (_, _) => await OnOpenedAsync(load);
    }

    /// <summary>
    /// Wires the Blueprints panel to the window: its messages, pasting a blueprint, its dialogs and file picker.
    /// </summary>
    private void WireBlueprints()
    {
        Blueprints.Message += Msg;
        Blueprints.Pasting += () => { ViewPanelCard.IsVisible = true; StartPaste(); };
        Blueprints.AskDetails = (start, cost) => Dialogs.AskBlueprint(this, start, cost);
        Blueprints.Models = () => _models;
        Blueprints.Confirm = text => Dialogs.Ask(this, Lang.T("rail.label.blueprints"), text, Lang.T("common.yes"));
        Blueprints.OpenUrl = Launcher.LaunchUriAsync;
        Blueprints.PickFile = () =>
        {
            var (title, type) = ImportPicker();
            return Pickers.File(StorageProvider, title, type);
        };
    }

    /// <summary>
    /// The import picker's title and file type, in the language in use (asked for each time it opens).
    /// </summary>
    internal static (string Title, Avalonia.Platform.Storage.FilePickerFileType Type) ImportPicker()
    {
        var type = new Avalonia.Platform.Storage.FilePickerFileType(Lang.T("rail.label.blueprints")) { Patterns = BlueprintPatterns };
        return (Lang.T("blueprints.pickTitle"), type);
    }

    /// <summary>
    /// Wires the Inspector to the window: its messages, the object it replaced, its dialog, and the View panel back
    /// when it closes.
    /// </summary>
    private void WireInspector()
    {
        Inspector.Message += said => { Msg(said); UpdateSaveBar(); };
        Inspector.Replaced += i => View.Select([i]);
        Inspector.Confirm = (title, text, yes) => Dialogs.Ask(this, title, text, yes);
        Inspector.Closed += () => ViewPanelCard.IsVisible = true;
    }
}
