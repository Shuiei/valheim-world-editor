using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The Workshop in the window (Workshop.cs): a blank plot opened like an area, its building saved as a
// Homestead blueprint (Save blueprint), back to the start page (Start), and the support check: each
// building piece outlined in the colour the game gives its support, pieces that would fall in pink.
public partial class MainWindow
{
    /// <summary>
    /// The cut: the building seen from a height (1 m steps, a half wall's height), to build inside it. 0..40 m above
    /// the plot, 0 the ground; the right end (<see cref="CutOff"/>) is off.
    /// </summary>
    internal Slider CutSlider { get; } = new() { Minimum = 0, Maximum = CutOff, Value = CutOff, SmallChange = 1, LargeChange = 2, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 120, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>
    /// The cut slider's right end: no cut.
    /// </summary>
    internal const double CutOff = 40;
    /// <summary>
    /// The cut's height, or that it is off.
    /// </summary>
    internal TextBlock CutText { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Width = 74 };
    /// <summary>
    /// The cut's slider and text, shown in the Workshop.
    /// </summary>
    internal StackPanel CutBox { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 6, IsVisible = false };

    /// <summary>
    /// Turns the support check on or off; shown in the Workshop.
    /// </summary>
    internal CheckBox SupportBox { get; } = new CheckBox { Content = Lang.T("workshop.label.support"), FontSize = 12, IsVisible = false, IsChecked = true, VerticalAlignment = VerticalAlignment.Center }.Classed("switch");

    /// <summary>
    /// The building's name (null: not named yet, "My building" when saved).
    /// </summary>
    private string? _workshopName;
    /// <summary>
    /// The blueprint's details (name, description, tags); null for a blank plot.
    /// </summary>
    private Homestead.Details? _workshopDetails;
    /// <summary>
    /// How far the blueprint opened was lifted to put its lowest point on the ground (taken off again when it is
    /// saved).
    /// </summary>
    private float _workshopLift;
    /// <summary>
    /// The ground the blueprints opened or added stood on in game (per metre; Workshop.GroundAt).
    /// </summary>
    private Dictionary<(int, int), float> _workshopTerrain = [];
    /// <summary>
    /// The file the blueprint was opened from or saved to (saving there again does not ask before replacing it); null
    /// when it is not in Homestead's own folder.
    /// </summary>
    private string? _workshopFile;
    /// <summary>
    /// The edits' version when the building was last saved (or opened).
    /// </summary>
    private int _workshopSaved;

    /// <summary>
    /// Whether the Workshop's plot is open.
    /// </summary>
    internal bool InWorkshop { get; private set; }
    /// <summary>
    /// The building on the plot changed since it was last saved as a blueprint (or opened).
    /// </summary>
    internal bool WorkshopDirty => InWorkshop && Session != null && Session.Edits.Version != _workshopSaved;

    /// <summary>
    /// The last support check (null: off, or not in the Workshop).
    /// </summary>
    internal Stability.Result? LastSupport { get; private set; }

    /// <summary>
    /// The things (indices in the scene) the last support check's pieces are, in its order.
    /// </summary>
    internal IReadOnlyList<int> LastSupportThings { get; private set; } = [];

    /// <summary>
    /// Wires the cut slider, the support switch and the Blueprints panel's Edit to the Workshop.
    /// </summary>
    private void SetUpWorkshop()
    {
        SupportBox.Tip("workshop.support");
        CutBox.Children.Add(CutSlider);
        CutBox.Children.Add(CutText);
        CutSlider.Tip("workshop.cut");
        CutSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty)
            {
                SetCut((float)CutSlider.Value);
            }
        };
        CutText.Text = Lang.T("workshop.cutOff");
        SupportBox.IsCheckedChanged += (_, _) => RefreshSupport();
        Blueprints.EditAsked += async path => await OpenWorkshop(path);
    }

    /// <summary>
    /// Wires the Build panel's Library (once the panel is made): open, add and import blueprints, and dungeons onto
    /// the plot.
    /// </summary>
    private void SetUpLibrary()
    {
        BuildPanel.Folder = () => Blueprints.Status.Folder;
        BuildPanel.OpenAsked += async path => await OpenWorkshop(path);
        BuildPanel.AddAsked += path => AddToWorkshop(path, null);
        BuildPanel.Dungeon.PlotAsked += async (settings, result, add) =>
        {
            try
            {
                await GeneratedOntoPlot(settings, result, add);
            }
            catch (Exception ex)
            {
                Msg("workshop.msg.dungeonFailed", ex.Message);
            }
        };
        BuildPanel.ImportAsked += async () =>
        {
            if (await Blueprints.PickFile() is string file && Blueprints.Import(file) != null)
            {
                BuildPanel.RenderLibrary();
            }
        };
    }

    /// <summary>
    /// Drag and drop onto the 3D view: a blueprint from the Library, or .blueprint and .vbuild files from the
    /// computer's files (imported into the library first). In the Workshop each goes where it is dropped; in a world,
    /// files are only imported.
    /// </summary>
    private void SetUpDrops(Control surface)
    {
        DragDrop.SetAllowDrop(surface, true);
        surface.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            bool ours = e.DataTransfer.Contains(BuildPanel.BlueprintFormat) && InWorkshop || e.DataTransfer.Contains(DataFormat.File);
            e.DragEffects = ours ? DragDropEffects.Copy : DragDropEffects.None;
        });
        surface.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = (e.DataTransfer.TryGetFiles() ?? [])
                .Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().ToList();
            Drop(e.DataTransfer.TryGetValue(BuildPanel.BlueprintFormat), files, e.GetPosition(surface), surface.Bounds.Size);
        });
    }

    /// <summary>
    /// What was dropped at a point of the view.
    /// </summary>
    /// <param name="blueprint">A Library blueprint's path, or null.</param>
    /// <param name="files">Files from the computer; only .blueprint and .vbuild ones are taken.</param>
    /// <param name="at">Where it was dropped, on the view.</param>
    /// <param name="size">The view's size, to find the ground under that point.</param>
    internal void Drop(string? blueprint, IReadOnlyList<string> files, Avalonia.Point at, Avalonia.Size size)
    {
        Vector2? world = null;
        if (Session is { } s && View.GridAt(at, size) is { } g)
        {
            world = new Vector2(s.Scene.OriginX + g.X, s.Scene.OriginZ + g.Y);
        }
        var paths = new List<string>();
        if (blueprint != null)
        {
            paths.Add(blueprint);
        }
        foreach (string f in files.Where(f => f.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase)))
        {
            if (Blueprints.Import(f) is string name)
            {
                paths.Add(Path.Combine(Blueprints.Status.Folder, Homestead.FileName(name)));
            }
        }
        if (files.Count > 0 && paths.Count == (blueprint != null ? 1 : 0))
        {
            Msg("workshop.msg.dropFiles");
            return;
        }
        BuildPanel.RenderLibrary();
        if (!InWorkshop)
        {
            return;
        }
        foreach (string p in paths)
        {
            AddToWorkshop(p, world);
        }
    }

    /// <summary>
    /// A blueprint added to the building on the plot (at a world point x, z; null: the middle), turned by degrees,
    /// as one step named label (null: "Added" and its name). Only in the Workshop.
    /// </summary>
    /// <param name="path">The blueprint file (its name picks the format).</param>
    /// <param name="at">Where its anchor goes (world x, z); null for the plot's middle.</param>
    /// <param name="turn">Degrees it is turned round its anchor.</param>
    /// <param name="label">The history's name for the step; null: "Added" and its name.</param>
    /// <param name="text">The blueprint's text when it is not a file of the Library; null reads <paramref name="path"/>.</param>
    internal void AddToWorkshop(string path, Vector2? at, float turn = 0, string? label = null, string? text = null)
    {
        if (!InWorkshop || Session is not { } s)
        {
            return;
        }
        try
        {
            var (placed, unknown, _, terrain) = Workshop.Add(s, path, text, at, turn, label: label);
            foreach (var (cell, y) in terrain)
            {
                _workshopTerrain[cell] = _workshopTerrain.TryGetValue(cell, out float was) ? MathF.Min(was, y) : y;
            }
            RefreshSupport();
            string name = (text == null ? Homestead.Read(path) : Homestead.Read(path, text.Split('\n')))?.Name ?? Path.GetFileNameWithoutExtension(path);
            Msg(Said.Of("workshop.msg.added", name, placed) + UnknownLeftOut(unknown) + Said.Of("workshop.msg.undoHint"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Msg("workshop.msg.addFailed", ex.Message);
        }
        UpdateSaveBar();
    }

    /// <summary>
    /// The Workshop, empty or with a blueprint's pieces on it. Leaves the open world first (asking when something is
    /// not saved), and a building not saved as a blueprint (asking too).
    /// </summary>
    /// <param name="path">The blueprint to open, or null for a blank plot.</param>
    internal async Task OpenWorkshop(string? path)
    {
        // A second open while one is under way (a double click while the game's files are read) would ask and show
        // the plot twice.
        if (_openingWorkshop)
        {
            Msg("workshop.msg.alreadyOpening");
            return;
        }
        _openingWorkshop = true;
        try
        {
            await OpenWorkshopOnce(path);
        }
        finally
        {
            _openingWorkshop = false;
        }
    }

    /// <summary>
    /// True while <see cref="OpenWorkshop"/> runs.
    /// </summary>
    private bool _openingWorkshop;

    /// <summary>
    /// <see cref="OpenWorkshop"/>'s work, once no other open is under way.
    /// </summary>
    /// <param name="path">The blueprint to open, or null for a blank plot.</param>
    private async Task OpenWorkshopOnce(string? path)
    {
        // The game's files are needed here too (its pieces, and how ground discs shape the plot).
        bool ready = await Task.Run(() =>
        {
            if (!GameData.Ensure(GameLook.Bundles))
            {
                return false;
            }
            // The player's mods' data names and manifests (their pieces' groups, the ones not offered).
            WorldSession.ReadModFiles();
            return true;
        });
        if (!ready)
        {
            if (World == null && !InWorkshop)
            {
                ShowStart(GameData.Missing);
            }
            else
            {
                MessageText.Text = GameData.Missing;
            }
            return;
        }
        if (World is { } w && w.Pending is not (0, 0, 0, 0)
            && !await Ask(Lang.T("workshop.ask.leaveWorld.title"),
                w.IsLive ? Lang.T("workshop.ask.leaveWorld.textLive", Describe(w.Pending)) : Lang.T("workshop.ask.leaveWorld.text", Describe(w.Pending)),
                Lang.T("common.leaveAnyway"), Lang.T("common.stay")))
        {
            return;
        }
        if (WorkshopDirty && !await Ask(Lang.T("workshop.name"), Lang.T("workshop.ask.openAnother.text"), Lang.T("workshop.ask.openAnother.yes"), Lang.T("common.stay")))
        {
            return;
        }
        string? name = null, text = null;
        Homestead.Details? details = null;
        if (path != null)
        {
            try
            {
                text = await File.ReadAllTextAsync(path);
                var parsed = BlueprintFormats.Parse(path, text);
                name = parsed.Name;
                details = Homestead.Read(path)?.Details is { } d ? d with { Name = name } : new Homestead.Details(name, parsed.Description ?? "", []);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The world stays open as it was.
                Msg("workshop.msg.openFailed", ex.Message);
                return;
            }
        }
        // Left only once the blueprint could be read.
        if (World is { } open)
        {
            // An area still loading in it is not opened, nor the one left open again.
            _navigation++;
            open.Dispose();
            World = null;
            Session = null;
            Tunnel.Close();
        }
        // The area's name stays English: Claude reads it (editor_state); the title shows it in the user's language.
        var scene = Workshop.Create(name != null ? $"Workshop · {name}" : "Workshop");
        await ShowEditor(scene);
        TitleText.Text = name != null ? Lang.T("workshop.title", name) : Lang.T("workshop.name");
        InWorkshop = true;
        _workshopName = name;
        _workshopLift = 0;
        _workshopTerrain = [];
        _workshopDetails = details;
        // Only Homestead's own folder is saved back to without asking (however its path is written: a separator at the
        // end, a link, Windows' letter case), and then by the path the editor saves under.
        _workshopFile = path != null && Path.GetDirectoryName(Path.GetFullPath(path)) is { } dir
            && Places.FolderKey(dir) == Places.FolderKey(Blueprints.Status.Folder)
            ? Path.Combine(Blueprints.Status.Folder, Path.GetFileName(path)) : null;
        var msg = Said.Of("workshop.msg.welcome");
        if (path != null)
        {
            var (placed, unknown, lift, terrain) = Workshop.Open(scene.Session!, path, text);
            _workshopLift = lift;
            _workshopTerrain = terrain;
            msg = Said.Of("workshop.msg.opened", name, placed) + UnknownLeftOut(unknown) + Said.Of("workshop.msg.changeIt");
        }
        _workshopSaved = scene.Session!.Edits.Version;
        EnterBuilding();
        MapButton.Content = Icons.With("back", Lang.T("workshop.label.start"));
        MapButton.IsVisible = true;
        SupportBox.IsVisible = true;
        DiscardButton.IsVisible = false;
        ShowWorkshopCost();
        Tools.ChooseMode(ToolMode.Place);
        BuildPanel.Card.IsVisible = true;
        RefreshSupport();
        UpdateSaveBar();
        Msg(msg);
    }

    /// <summary>
    /// The sentence naming the kinds the game does not know that a blueprint put on the plot left out (the first five),
    /// empty when there are none.
    /// </summary>
    /// <param name="unknown">The kinds left out (game data, the same in every language).</param>
    private static Said UnknownLeftOut(List<string> unknown) =>
        unknown.Count > 0 ? Said.Of("blueprints.msg.unknownImported", unknown.Count, string.Join(", ", unknown.Take(5))) : Said.Empty;

    /// <summary>
    /// Leaving an area: no Workshop any more (ShowEditor calls it for every area).
    /// </summary>
    private void ResetWorkshop()
    {
        LeaveBuilding();
        InWorkshop = false;
        ToolTip.SetTip(SubtitleText, null);
        MapButton.Content = Icons.With("back", Lang.T("workshop.label.map"));
        SupportBox.IsVisible = false;
        DiscardButton.IsVisible = true;
        LastSupport = null;
        View.ShowSupport(null);
    }

    /// <summary>
    /// Start: back to the start page, asking first when the building is not saved as a blueprint.
    /// </summary>
    internal async Task LeaveWorkshop()
    {
        if (WorkshopDirty && !await Ask(Lang.T("workshop.ask.leave.title"), Lang.T("workshop.ask.leave.text"), Lang.T("common.leaveAnyway"), Lang.T("common.stay")))
        {
            return;
        }
        ResetWorkshop();
        Session = null;
        ShowStart();
    }

    /// <summary>
    /// Save blueprint: the building pieces on the plot, as a Homestead blueprint (asks first when the support check
    /// says some would fall in game).
    /// </summary>
    internal async Task SaveWorkshop()
    {
        if (Session is not { } s)
        {
            return;
        }
        View.SelectTool.Commit();
        if (Workshop.Pieces(s.Scene) == 0)
        {
            await Tell(Lang.T("workshop.noPiecesToSave"));
            return;
        }
        RefreshSupport();
        if (LastSupport is { Breaking: > 0 } r
            && !await Ask(Lang.T("workshop.label.saveBlueprint"), Lang.T("workshop.ask.falling.text", r.Breaking), Lang.T("workshop.ask.falling.yes"), Lang.T("common.keepBuilding")))
        {
            return;
        }
        string name = _workshopName ?? Lang.T("workshop.defaultName");
        var clip = Workshop.Building(s.Scene, name, _workshopLift);
        if (await Blueprints.SaveBuilding(clip, _workshopDetails ?? new Homestead.Details(name, "", []), _workshopFile) is { } details)
        {
            WorkshopSavedAs(s, details);
        }
    }

    /// <summary>The plot's building is saved under these details: the Workshop takes its name and is no longer dirty.</summary>
    private void WorkshopSavedAs(EditSession s, Homestead.Details details)
    {
        _workshopName = details.Name;
        _workshopDetails = details;
        _workshopFile = Path.Combine(Blueprints.Status.Folder, Homestead.FileName(details.Name));
        BuildPanel.RenderLibrary();
        _workshopSaved = s.Edits.Version;
        TitleText.Text = Lang.T("workshop.title", details.Name);
        UpdateSaveBar();
    }

    /// <summary>
    /// Save blueprint without asking (Claude's save_blueprint): the plot's building under these details. Refused when
    /// there is no building piece, when pieces would fall in game (unless evenIfFalling), or when another blueprint of
    /// that name exists (unless overwrite; the one the building was opened from or last saved as may always be saved
    /// over).
    /// </summary>
    /// <returns>Null when saved, else why not.</returns>
    internal string? SaveWorkshopAs(Homestead.Details details, bool overwrite, bool evenIfFalling)
    {
        if (!InWorkshop || Session is not { } s)
        {
            return "Blueprints are saved from the Workshop: open_workshop first.";
        }
        View.SelectTool.Commit();
        if (Workshop.Pieces(s.Scene) == 0)
        {
            return "There is no building piece on the plot to save.";
        }
        // Worked out even when the Support check is off: its switch only shows the colours.
        if (SolveSupport(s).Result is { Breaking: > 0 } r && !evenIfFalling)
        {
            return $"{r.Breaking} piece(s) would fall in game (support_check lists them): fix them, or save anyway with evenIfFalling.";
        }
        // The library's folder as it is now (Homestead may have been installed or moved since).
        Blueprints.Refresh();
        string path = Path.Combine(Blueprints.Status.Folder, Homestead.FileName(details.Name));
        if (File.Exists(path) && !overwrite && !string.Equals(path, _workshopFile, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return $"There is a blueprint called {details.Name} already: ask the user before replacing it (overwrite), or choose another name.";
        }
        var clip = Workshop.Building(s.Scene, details.Name, _workshopLift);
        if (Blueprints.SaveBuildingAs(clip, details) is string problem)
        {
            return problem;
        }
        WorkshopSavedAs(s, details);
        return null;
    }

    /// <summary>
    /// The support check over the plot's building pieces (Stability), shown as coloured outlines; pieces that would
    /// fall in pink.
    /// </summary>
    internal void RefreshSupport()
    {
        if (!InWorkshop || SupportBox.IsChecked != true || Session is not { } s)
        {
            LastSupport = null;
            View.ShowSupport(null);
            return;
        }
        var (r, index) = SolveSupport(s);
        LastSupport = r;
        LastSupportThings = index;
        var show = new Dictionary<int, float>();
        for (int k = 0; k < index.Count; k++)
        {
            if (!r.Free[k])
            {
                show[index[k]] = r.Breaks[k] ? -2 : r.Colour[k];
            }
        }
        View.ShowSupport(show);
    }

    /// <summary>
    /// The support check over the plot's building pieces (Stability), whether or not its colours are shown.
    /// </summary>
    /// <returns>The result, and the scene's index of each piece it was worked out for.</returns>
    internal (Stability.Result Result, List<int> Index) SolveSupport(EditSession s)
    {
        var index = new List<int>();
        var pieces = new List<Stability.Piece>();
        lock (s.Scene.Things)
        {
            for (int i = 0; i < s.Scene.Things.Count; i++)
            {
                var t = s.Scene.Things[i];
                // Buildable pieces only (rocks and other things the hoe places are not the building).
                if (t.Gone || PrefabCatalog.NameOf(t.Prefab) is not string name || !Workshop.Buildable(name))
                {
                    continue;
                }
                index.Add(i);
                pieces.Add(new Stability.Piece(name, t.Position, BlueprintFormats.FromEuler(t.Rotation), t.Scale > 0 ? t.Scale : 1));
            }
        }
        var terrain = _workshopTerrain;
        return (Stability.Solve(pieces, (x, z) => Workshop.GroundAt(terrain, x, z)), index);
    }

    /// <summary>
    /// What the building on the plot costs to build in game, under the title (all of it in the tip).
    /// </summary>
    internal string WorkshopCost { get; private set; } = "";

    /// <summary>
    /// Shows the building's cost under the title, the full list of materials and stations in its tip.
    /// </summary>
    private void ShowWorkshopCost()
    {
        if (!InWorkshop || Session is not { } s)
        {
            return;
        }
        var cost = PieceCost.Of(Workshop.Kinds(s.Scene));
        WorkshopCost = cost.Describe(5);
        SubtitleText.Text = Workshop.Pieces(s.Scene) == 0 ? Lang.T("workshop.blankPlot") : Lang.T("blueprints.cost", WorkshopCost);
        ToolTip.SetTip(SubtitleText, cost.Materials.Count == 0 ? null
            : string.Join("\n", cost.Materials.Select(m => $"{m.Name}: {m.Amount}")) + (cost.Stations.Count > 0 ? "\n" + Lang.T("workshop.near", string.Join(", ", cost.Stations)) : "")
                + (cost.Unknown.Count > 0 ? "\n" + Lang.T("workshop.noRecipe", string.Join(", ", cost.Unknown.Keys.Take(5))) : ""));
    }

    /// <summary>
    /// The save bar in the Workshop: the piece count, how many would fall, whether it is saved, and Save blueprint.
    /// </summary>
    private void WorkshopSaveBar(EditSession s)
    {
        ShowWorkshopCost();
        int n = Workshop.Pieces(s.Scene);
        bool dirty = WorkshopDirty;
        string? falls = LastSupport is { Breaking: > 0 } r ? Lang.T("workshop.bar.falling", r.Breaking) : null;
        PendingText.Text = n == 0 ? Lang.T("workshop.bar.none")
            : string.Join(" · ", new[] { Lang.T("workshop.bar.pieces", n), falls, dirty ? Lang.T("workshop.bar.notSaved") : Lang.T("workshop.bar.saved") }.OfType<string>());
        SaveButton.IsEnabled = n > 0;
        SaveButton.Content = Lang.T("workshop.label.saveBlueprint");
        SaveButton.Tip("top.saveBlueprint");
        SetPendingPill(dirty);
        _liveBadge.IsVisible = false;
        UndoButton.IsEnabled = s.CanUndo;
        RedoButton.IsEnabled = s.CanRedo;
    }

    /// <summary>
    /// After a change Claude made (ClaudeTools): the save bar, the support check, the message.
    /// </summary>
    internal void AfterClaudeEdit(string message)
    {
        MessageText.Text = message;
        UpdateSaveBar();
        QueueSupport();
    }

    /// <summary>
    /// True while a support check is queued.
    /// </summary>
    private bool _supportQueued;

    /// <summary>
    /// After an edit in the Workshop: the support check again (a moment later, once per batch of edits).
    /// </summary>
    private void QueueSupport()
    {
        if (!InWorkshop || _supportQueued)
        {
            return;
        }
        _supportQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _supportQueued = false;
            RefreshSupport();
            if (Session is { } s)
            {
                WorkshopSaveBar(s);
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// What the Workshop changes and gives back when it closes: the Place tool's settings (Build sets them for building
    /// pieces) and the zone borders (the plot is no world).
    /// </summary>
    /// <param name="Chosen">The Place tool's chosen kinds.</param>
    /// <param name="Mode">Its mode.</param>
    /// <param name="OneAtATime">Whether it placed one at a time.</param>
    /// <param name="OneAtATimeByHand">Whether One at a time was set by hand.</param>
    /// <param name="RandomYaw">Whether it turned each object at random.</param>
    /// <param name="Tilt">The most it leaned an object at random, in degrees.</param>
    /// <param name="SizeMin">Its smallest random scale, in percent.</param>
    /// <param name="SizeMax">Its largest random scale, in percent.</param>
    /// <param name="Elevation">Where objects stood.</param>
    /// <param name="SnapTo">Whether pieces snapped to the pieces already there.</param>
    /// <param name="OnTop">Whether a single piece snapped on top of the piece under the cursor.</param>
    /// <param name="Rotation">The turn it added to every object's facing, in degrees.</param>
    /// <param name="Borders">Whether the zone borders were shown.</param>
    private sealed record Kept(List<string> Chosen, PlaceTool.Modes Mode, bool OneAtATime, bool OneAtATimeByHand, bool RandomYaw, float Tilt, float SizeMin,
        float SizeMax, PlaceTool.Elevations Elevation, bool SnapTo, bool OnTop, float Rotation, bool Borders);
    // (The unsaved marks: everything on the plot is new, so they would only cover the building.)

    /// <summary>
    /// The Place tool's settings kept while building; null when not building.
    /// </summary>
    private Kept? _kept;

    /// <summary>
    /// The Place tool's settings, kept while the Workshop's Build (or building in a dungeon) uses it.
    /// </summary>
    private Kept KeepPlaceTool()
    {
        var t = PlaceTool;
        return new Kept([.. t.Chosen], t.Mode, t.OneAtATime, t.OneAtATimeByHand, t.RandomYaw, t.Tilt, t.SizeMin, t.SizeMax, t.Elevation, t.SnapTo, t.OnTop, t.Rotation,
            View.IsOverlayShown(Overlays.Layer.Borders));
    }

    /// <summary>
    /// The Place tool as it was kept, out of Build.
    /// </summary>
    private void RestorePlaceTool(Kept k)
    {
        var t = PlaceTool;
        t.Chosen.Clear();
        t.Chosen.AddRange(k.Chosen);
        (t.Mode, t.OneAtATime, t.OneAtATimeByHand, t.RandomYaw, t.Tilt, t.SizeMin, t.SizeMax, t.Elevation, t.SnapTo, t.OnTop, t.Rotation) =
            (k.Mode, k.OneAtATime, k.OneAtATimeByHand, k.RandomYaw, k.Tilt, k.SizeMin, k.SizeMax, k.Elevation, k.SnapTo, k.OnTop, k.Rotation);
        t.GridStep = 0;
        t.Building = false;
        t.HeightNudge = 0;
        t.AimRay = null;
        t.CutY = null;
        PlaceInput.TurnStep = null;
    }

    /// <summary>
    /// The world tools away (the rail keeps Build, Select and View), the View panel and the Mask closed, no zone
    /// borders; the Place tool set for building.
    /// </summary>
    private void EnterBuilding()
    {
        // Building in a dungeon has ended already (ShowEditor's Edit ends it), so the settings kept are the Place tool's own.
        _kept ??= KeepPlaceTool();
        Tools.SetWorkshop(true);
        View.SetOverlay(Overlays.Layer.Borders, false);
        View.ShowNewMarkers = false;
        CutBox.IsVisible = true;
        CutSlider.Value = CutOff;
        // Solid pieces (the View panel and its See-through switch are away).
        View.SeeThroughBuildings = false;
        ShowRight(null);
        ViewButton.IsVisible = false;
        MaskPanel.Card.IsVisible = false;
        BuildPanel.Start();
    }

    /// <summary>
    /// Out of building: the Place tool, the zone borders and the View panel as they were.
    /// </summary>
    private void LeaveBuilding()
    {
        if (_kept is not { } k)
        {
            return;
        }
        _kept = null;
        RestorePlaceTool(k);
        Tools.SetWorkshop(false);
        View.SetOverlay(Overlays.Layer.Borders, k.Borders);
        CutBox.IsVisible = false;
        CutSlider.Value = CutOff;
        View.ShowNewMarkers = UnsavedBox.IsChecked == true;
        View.SeeThroughBuildings = SeeThroughBox.IsChecked == true;
        ViewButton.IsVisible = true;
        BuildPanel.Card.IsVisible = false;
        PlacePanel.Fill();
        PlaceTool.Notify();
    }

    /// <summary>
    /// The cut at so many metres above the plot (0: the ground; <see cref="CutOff"/>: none): nothing drawn above it,
    /// and the cursor goes through what it hides.
    /// </summary>
    private void SetCut(float metres)
    {
        float? y = metres < CutOff ? Workshop.Ground + metres : null;
        View.CutY = y;
        PlaceTool.CutY = y;
        CutText.Text = metres < CutOff ? Lang.T("workshop.cutAt", metres) : Lang.T("workshop.cutOff");
        PlaceInput.Refresh();
    }
}
