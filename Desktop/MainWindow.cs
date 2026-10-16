using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The editor's window: its pages (the start page, a new world, the world map, and the 3D editor of an area or the
/// Workshop), the top and save bars, the tool panels, and the help card with the frame rate, what is loaded, and the
/// Record frame rates switch.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// The help card's frame rate, and what was loaded (newest first).
    /// </summary>
    private readonly TextBlock _fps = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap }, _info = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
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
    internal Button ViewButton { get; } = new Button { Content = Icons.With("view", "View"), FontSize = 12.5 }.Classed("ghost");
    /// <summary>
    /// The top bar's names: the world, and the area open.
    /// </summary>
    private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, Text = "Valheim World Editor", TextTrimming = TextTrimming.CharacterEllipsis };
    /// <summary>
    /// Under the title: the area open, or in the Workshop the building's cost.
    /// </summary>
    private readonly TextBlock _subtitle = new() { FontSize = 12, Foreground = Ui.Muted, TextTrimming = TextTrimming.CharacterEllipsis };
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
                new TextBlock { Text = "LIVE", FontSize = 11.5, FontWeight = FontWeight.Bold, LetterSpacing = 0.7, Foreground = new SolidColorBrush(Color.Parse("#8ff0b4")) },
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
    internal Button SaveButton { get; } = new Button { Content = "Save to world", FontSize = 12.5, IsEnabled = false }.Classed("primary");
    /// <summary>
    /// Opens the History panel.
    /// </summary>
    internal Button HistoryButton { get; } = new Button { Content = Icons.With("history", "History"), FontSize = 12.5 }.Classed("ghost");
    /// <summary>
    /// The History panel: the edits so far, to go back to one.
    /// </summary>
    internal HistoryPanel History { get; }
    /// <summary>
    /// Back to the map (in the Workshop: to the start page).
    /// </summary>
    internal Button MapButton { get; } = new Button { Content = Icons.With("back", "Map"), FontSize = 12.5, IsVisible = false }.Classed("ghost");

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
    private static string Describe((int Zones, int Deleted, int Added, int Resets) p)
    {
        var (z, d, a, r) = p;
        return string.Join(", ", new[] { z > 0 ? $"{z} zone(s) of ground" : "", d > 0 ? $"{d} deleted" : "", a > 0 ? $"{a} added" : "", r > 0 ? $"{r} zone reset" : "" }.Where(x => x != ""));
    }

    /// <summary>
    /// Whether the busy veil is up.
    /// </summary>
    internal bool IsBusy => _busy.IsVisible;

    /// <summary>
    /// The busy veil with what is being done, or none (null).
    /// </summary>
    private void Busy(string? text)
    {
        _busyText.Text = text ?? "";
        _busy.IsVisible = text != null;
    }

    /// <summary>
    /// A new world: the seed's preview and search, then Create (a world in the game's folder, opened). Null when that
    /// page is not shown.
    /// </summary>
    internal NewWorldPage? NewWorld { get; private set; }

    /// <summary>
    /// Shows a new New world page.
    /// </summary>
    internal void ShowNewWorld()
    {
        var page = NewWorld = new NewWorldPage();
        page.BackRequested += () => { page.Dispose(); NewWorld = null; ShowStart(); };
        page.CreateRequested += async (folder, name, seed) => await CreateWorld(folder, name, seed);
        Title = $"Valheim World Editor {BuildInfo.Version} · A new world";
        _pages.Content = page.View;
    }

    /// <summary>
    /// Makes a world in <paramref name="folder"/> and opens it; on failure the New world page says why.
    /// </summary>
    /// <returns>Null when made, else why it could not be.</returns>
    internal async Task<string?> CreateWorld(string folder, string name, string seed)
    {
        Busy($"Making the world {name}…");
        try
        {
            await Task.Run(() => TerrainEditor.Save.WorldCreator.Create(folder, name, seed));
            Console.WriteLine($"New world: made {name} (seed {seed}) in {folder}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Console.WriteLine($"New world: could not make {name} (seed {seed}) in {folder}: {ex}");
            Busy(null);
            if (NewWorld is { } p)
            {
                p.Problem.Text = $"Could not make the world: {ex.Message}";
            }
            return $"Could not make the world: {ex.Message}";
        }
        Busy(null);
        NewWorld?.Dispose();
        NewWorld = null;
        await OpenWorld(() => Task.Run(() => WorldSession.Open(folder)), $"Opening {name}…");
        string made = $"Made {name} (seed {seed}). The game lays out its start, traders and dungeons the first time it loads it, and makes each place as players reach it: play it before reshaping an area here.";
        // Opened: on its map, where the status bar is not shown.
        if (World != null)
        {
            Say(made);
        }
        else
        {
            MessageText.Text = made;
        }
        return null;
    }

    /// <summary>
    /// The start page: how to edit, and which world.
    /// </summary>
    /// <param name="error">Shown on a newly made start page; null to keep the page made before.</param>
    internal void ShowStart(string? error = null)
    {
        MapPage?.Stop();
        if (StartPage == null || error != null)
        {
            StartPage?.Stop();
            StartPage = new StartPage(Settings, error);
            StartPage.OpenRequested += async (open, what) => await OpenWorld(open, what);
            StartPage.WorkshopRequested += async path => await OpenWorkshop(path);
            StartPage.NewWorldRequested += ShowNewWorld;
            StartPage.OpenUrl = Launcher.LaunchUriAsync;
            StartPage.SettingsRequested += async () =>
            {
                if (await SettingsDialog.Show(this, Settings))
                {
                    StartPage!.SetMode(StartPage.Mode);
                    // Claude's connection, as the settings now say.
                    await ClaudeServer.Apply(Settings, this);
                }
            };
            StartPage.Confirm = text => Dialogs.Ask(this, "Valheim World Editor", text, "Yes");
            StartPage.PickFolder = title => Pickers.Folder(StorageProvider, title);
            StartPage.PickFile = title => Pickers.File(StorageProvider, title);
        }
        else
        {
            StartPage.SetMode(StartPage.Mode);
        }
        Title = $"Valheim World Editor {BuildInfo.Version}";
        _pages.Content = StartPage.View;
    }

    /// <summary>
    /// Opens a world (offline or live), then shows its map; back to the start page, with why, when it cannot be opened.
    /// </summary>
    /// <param name="open">Opens it (off the UI thread).</param>
    /// <param name="what">What the busy veil says meanwhile.</param>
    internal async Task OpenWorld(Func<Task<WorldSession>> open, string what)
    {
        Busy(what);
        try
        {
            World = await open();
        }
        catch (Exception ex)
        {
            Busy(null);
            Tunnel.Close();
            ShowStart($"Could not open the world: {ex.Message}");
            return;
        }
        finally
        {
            Busy(null);
        }
        StartPage?.Stop();
        ShowMap();
    }

    /// <summary>
    /// The world map (what is not saved stays pending).
    /// </summary>
    internal void ShowMap()
    {
        if (World == null)
        {
            ShowStart();
            return;
        }
        _navigation++;
        View.SelectTool.Commit();
        KeepHistory();
        _playersTimer.Stop();
        _labelsTimer.Stop();
        // Following the game is the open area's: the map reads the world as it is (Reload for the rest).
        _gameTimer.Stop();
        _gameFetched = null;
        PlayerLabels.Children.Clear();
        if (MapPage == null)
        {
            MapPage = new MapPage
            {
                StartView = () => Prefs.TryGet("map.view", out float[]? v) && v is { Length: 3 } && v.All(float.IsFinite) && v[2] > 0 ? (v[0], v[1], Math.Clamp(v[2], 0.25f, 40f)) : null
            };
            MapPage.Map.ViewChanged += () => Prefs.Set("map.view", new[] { MapPage.Map.Center.X, MapPage.Map.Center.Y, MapPage.Map.MetersPerPixel });
            MapPage.BackToWorlds += async () => await LeaveWorld();
            MapPage.EditRequested += async (x, z, size) => await EditArea(x, z, size);
            MapPage.SaveRequested += async () => await SaveWorld();
            MapPage.DiscardRequested += async () => await DiscardWorld();
            MapPage.ReloadRequested += async () => await ReloadWorld();
            MapPage.Confirm = text => Dialogs.Ask(this, "Valheim World Editor", text, "Yes");
            MapPage.Tell = text => Dialogs.Tell(this, "Valheim World Editor", text);
        }
        Title = $"{World.World.Name} · Valheim World Editor {BuildInfo.Version}";
        MapPage.Show(World);
        _pages.Content = MapPage.View;
    }

    /// <summary>
    /// Leaving an area: its history stays with the world, for the next area opened, and the area is closed, so work
    /// awaited in it (a script, Grow) finds it gone and changes nothing.
    /// </summary>
    /// <returns>The area left, or null when none was open in the 3D editor.</returns>
    private EditSession? KeepHistory()
    {
        if (Session is { Scene.Owner: { } owner } s && _pages.Content == _editorPage)
        {
            owner.History = s.Export();
            owner.Area = null;
            Session = null;
            return s;
        }
        return null;
    }

    /// <summary>
    /// Counts the map shown, the areas asked for and the world left: an area load that finished after one of those is
    /// not shown, and one that failed leaves the area it left closed.
    /// </summary>
    private long _navigation;

    /// <summary>
    /// Another area could not be opened: the one left (still shown) is open again, its history its own, unless the map
    /// or another area was asked for meanwhile.
    /// </summary>
    /// <param name="left">What <see cref="KeepHistory"/> returned.</param>
    /// <param name="navigation">The <see cref="_navigation"/> count of the failed load.</param>
    private void Unleave(EditSession? left, long navigation)
    {
        if (left is { Scene.Owner: { } owner } && navigation == _navigation && World == owner && _pages.Content == _editorPage
            && Session == null && View.Scene == left.Scene)
        {
            owner.Area = left;
            Session = left;
        }
    }

    /// <summary>
    /// An area of the world in the 3D editor; an object the map's search found there is selected, the camera on it.
    /// </summary>
    /// <param name="x">The middle zone, east.</param>
    /// <param name="z">The middle zone, north.</param>
    /// <param name="size">The area's side, in zones.</param>
    internal Task EditArea(int x, int z, int size) => EditBlock(x - size / 2, z - size / 2, size, size, (x, z));

    /// <summary>
    /// A block of zones of the world in the 3D editor (it need not be square: Follow grows it); an object the map's
    /// search found at <paramref name="spot"/> is selected, the camera on it.
    /// </summary>
    /// <param name="x0">The south-west zone, east.</param>
    /// <param name="z0">The south-west zone, north.</param>
    /// <param name="zonesX">Zones west to east.</param>
    /// <param name="zonesZ">Zones south to north.</param>
    /// <param name="spot">The map's chosen zone the area was opened around, if it was.</param>
    /// <param name="context">Said before the failure, when it cannot be opened.</param>
    /// <returns>
    /// Whether the block was opened (no: the failure is said in the status bar; when this was the last area asked for
    /// and the 3D editor is left with no area open, the map is shown and the failure in a dialog).
    /// </returns>
    internal async Task<bool> EditBlock(int x0, int z0, int zonesX, int zonesZ, (int X, int Z)? spot = null, string? context = null)
    {
        if (World is not { } world)
        {
            return false;
        }
        long navigation = ++_navigation;
        View.SelectTool.Commit();
        var left = KeepHistory();
        Busy($"Loading {zonesX} × {zonesZ} zones…");
        string failed;
        try
        {
            await BeforeLoad();
            var scene = await Task.Run(() => WorldScene.LoadBlock(world, x0, z0, zonesX, zonesZ));
            if (!await ShowEditor(scene, () => navigation == _navigation))
            {
                // The map or another area was asked for while it loaded: it is not shown, nor the world's area.
                if (world.Area == scene.Session)
                {
                    world.Area = Session;
                }
                Report("That area was not opened: the map or another area was asked for while it loaded.");
                return false;
            }
            // Shown: the world's area, even if a load dropped meanwhile had reset it.
            world.Area = scene.Session;
            // Found by the map's search: selected, the camera on it (items and texts: in the inspector).
            if (MapPage?.Target is var (id, inspect) && spot != null && MapPage.Spot == spot)
            {
                int i = scene.Things.FindIndex(t => t.Id == id && !t.Gone);
                if (i >= 0)
                {
                    View.Focus(scene.Things[i].Position);
                    View.Select([i]);
                    if (inspect)
                    {
                        Inspect();
                    }
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Unleave(left, navigation);
            failed = context + "Could not open that area: " + ex.Message;
            Report(failed);
        }
        finally
        {
            Busy(null);
        }
        // The last area asked for failed with none open behind the 3D editor (closed for this one, or for another that
        // failed too): the map, not tools that silently do nothing, and why in a dialog (the map has no status bar).
        if (navigation == _navigation && Session == null && _pages.Content == _editorPage)
        {
            ShowMap();
            await Tell(failed);
        }
        return false;

        // The world left meanwhile: only the status bar, no dialog about a world no longer open.
        void Report(string text)
        {
            if (World == world)
            {
                Say(text);
            }
            else
            {
                MessageText.Text = text;
            }
        }
    }

    /// <summary>
    /// Awaited before an area's zones are read (tests hold loads there to order them).
    /// </summary>
    internal Func<Task> BeforeLoad { get; set; } = () => Task.CompletedTask;

    /// <summary>
    /// Shows a loaded area (or the Workshop's plot) in the 3D editor, the game's models opened first if they are not
    /// yet.
    /// </summary>
    /// <param name="scene">The area or plot.</param>
    /// <param name="stillWanted">Asked once the models are open: false, nothing is shown.</param>
    /// <returns>Whether it was shown.</returns>
    internal async Task<bool> ShowEditor(WorldScene scene, Func<bool>? stillWanted = null)
    {
        if (_models == null && _modelsLoading != null)
        {
            _models = await _modelsLoading;
            _modelsLoading = null;
        }
        _models ??= await Task.Run(ModelStore.Open);
        if (stillWanted?.Invoke() == false)
        {
            return false;
        }
        ResetWorkshop();
        // Lines and shapes drawn in the last area are left behind.
        View.Path.Clear();
        View.Area.Clear();
        View.Tape.Clear();
        PlaceTool.ClearShape();
        Inspector.Close();
        _info.Text = scene.LoadInfo + (_models == null ? "\nNo game models: Valheim was not found, boxes stand in (choose its folder on the start page)." : "");
        View.Show(scene, _models);
        if (scene.Session != null)
        {
            Edit(scene.Session);
        }
        // The Area tool's lists follow the scene shown (its selection was cleared before, with the old one shown).
        AreaPanel.Refresh();
        MapButton.IsVisible = scene.Owner != null && !Options.Direct;
        _title.Text = scene.Name;
        _subtitle.Text = $"{scene.ZonesX} × {scene.ZonesZ} zones, from zone {scene.X0}, {scene.Z0} to {scene.X0 + scene.ZonesX - 1}, {scene.Z0 + scene.ZonesZ - 1} · {scene.Things.Count(t => !t.Gone):N0} objects";
        Title = $"{scene.Name} · Valheim World Editor {BuildInfo.Version}";
        _pages.Content = _editorPage;
        EditorOpened(scene);
        return true;
    }

    /// <summary>
    /// Map: Save (or Apply live) for the whole world; saving asks first, applying only when it would overwrite the
    /// game's changes.
    /// </summary>
    internal async Task SaveWorld()
    {
        if (World is not { } w)
        {
            return;
        }
        if (w.IsLive)
        {
            Busy("Applying to the running game…");
            string message;
            try
            {
                message = (await w.ApplyLive(over => { Busy(null); return ConfirmOverGame(over); })).Message;
            }
            catch (Exception ex)
            {
                message = "Could not apply live: " + ex.Message;
            }
            finally
            {
                Busy(null);
            }
            await Tell(message);
        }
        else
        {
            string what = Describe(w.Pending);
            if (!await ConfirmSave(SaveConfirmText(what, w.World.Directory)))
            {
                return;
            }
            Busy("Saving…");
            string msg;
            int? newer = null;
            try
            {
                var o = await Task.Run(w.Save);
                msg = o.Message;
                if (o.Saved is { } r)
                {
                    if (r.Skipped.Count > 0)
                    {
                        msg += "\n\nNot saved:\n• " + string.Join("\n• ", r.Skipped);
                    }
                }
                newer = o.Done ? null : w.GameSaveSince();
            }
            catch (Exception ex)
            {
                msg = "Could not save: " + ex.Message;
            }
            finally
            {
                Busy(null);
            }
            if (newer is int game)
            {
                await OfferReplay(w, game, null);
            }
            else
            {
                await Tell(msg);
            }
        }
        MapPage?.Show(w);
    }

    /// <summary>
    /// A save refused because the game saved the world after the editor read it: offers to bring the pending changes
    /// onto the game's save (WorldSession.Replay) instead of leaving them behind. Asked first; then, for each kind of
    /// change the game made too, whose version stays. Nothing is written: the changes wait on the game's save, to be
    /// checked and saved. The area open is read again.
    /// </summary>
    /// <param name="w">The world.</param>
    /// <param name="newest">The game's save number.</param>
    /// <param name="area">The area open in the 3D editor when Save was pressed there (null: from the map).</param>
    internal async Task OfferReplay(WorldSession w, int newest, WorldScene? area)
    {
        if (!await Ask(Lang.T("replay.title"), Lang.T("replay.offer", newest), Lang.T("replay.bring"), Lang.T("replay.notNow")))
        {
            Say(Lang.T("replay.notNowSaid", newest));
            return;
        }
        Busy(Lang.T("replay.reading"));
        EditReplay? plan;
        try
        {
            plan = await Task.Run(w.PrepareReplay);
        }
        catch (Exception ex)
        {
            Busy(null);
            await Tell(Lang.T("replay.failed", ex.Message));
            return;
        }
        Busy(null);
        if (plan == null)
        {
            await Tell(Lang.T("replay.failed", Lang.T("replay.noNewer")));
            return;
        }
        var keep = new Dictionary<ReplayClash, bool>();
        foreach (var kind in Enum.GetValues<ReplayClash>())
        {
            var clashes = plan.Clashes.Where(c => c.Kind == kind).ToList();
            if (clashes.Count > 0)
            {
                keep[kind] = await Ask(Lang.T("replay.clashTitle"), ClashText(kind, clashes), Lang.T("replay.keepMine"), Lang.T("replay.keepGame"));
            }
        }
        Busy(Lang.T("replay.applying"));
        WorldSession.Outcome o;
        try
        {
            o = await Task.Run(() => w.Replay(plan, k => keep.GetValueOrDefault(k)));
        }
        catch (Exception ex)
        {
            Busy(null);
            await Tell(Lang.T("replay.failed", ex.Message));
            return;
        }
        Busy(null);
        if (o.Reloaded && area != null && View.Scene == area && World == w)
        {
            await ReopenArea(area);
        }
        MessageText.Text = o.Message;
        await Tell(o.Message);
    }

    /// <summary>The question for one kind of change the game made too: what it is, the first few of them, and the two ways.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="clashes">The changes of that kind.</param>
    private string ClashText(ReplayClash kind, List<EditReplay.Clash> clashes)
    {
        const int Shown = 8;
        var lines = clashes.Take(Shown).Select(c => c.Kind == ReplayClash.Ground
            ? Lang.T("replay.zone", c.Zone.X, c.Zone.Z)
            : Lang.T("replay.object", NameOfPrefab(c.Prefab) ?? c.Prefab.ToString(System.Globalization.CultureInfo.InvariantCulture), c.Position.X, c.Position.Z)).ToList();
        if (clashes.Count > Shown)
        {
            lines.Add(Lang.T("replay.more", clashes.Count - Shown));
        }
        string list = "• " + string.Join("\n• ", lines);
        string text = kind switch
        {
            ReplayClash.Ground => Lang.T("replay.clash.ground", clashes.Count, list),
            ReplayClash.Changed => Lang.T("replay.clash.changed", clashes.Count, list),
            _ => Lang.T("replay.clash.removed", clashes.Count, list),
        };
        int unsure = clashes.Count(c => c.Unsure);
        return unsure > 0 ? text + "\n\n" + Lang.T("replay.unsure", unsure) : text;
    }

    /// <summary>
    /// Map: Discard, after asking: the unsaved changes dropped and the world read again.
    /// </summary>
    internal async Task DiscardWorld()
    {
        if (World is not { } w || !await Ask("Discard", $"Discard the unsaved changes ({Describe(w.Pending)})? The world is read again{(w.IsLive ? " from the game" : " from disk")}.", "Discard", "Keep them"))
        {
            return;
        }
        Busy("Reading the world again…");
        try
        {
            await Task.Run(w.Discard);
        }
        catch (Exception ex)
        {
            Say("Could not read the world again: " + ex.Message);
        }
        finally
        {
            Busy(null);
        }
        MapPage?.Show(w);
    }

    /// <summary>
    /// Map, live: reads the world from the game again.
    /// </summary>
    internal async Task ReloadWorld()
    {
        if (World is not { IsLive: true } w)
        {
            return;
        }
        Busy("Loading the world from the game…");
        try
        {
            await w.Reload();
        }
        // As ReloadHere: the bridge's answers (a refusal, broken JSON) too.
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException or System.Text.Json.JsonException)
        {
            Say("Could not reach the game: " + ex.Message);
        }
        finally
        {
            Busy(null);
        }
        MapPage?.Show(w);
    }

    /// <summary>
    /// Worlds: back to the start page (asks first when something is not saved).
    /// </summary>
    internal async Task LeaveWorld()
    {
        if (World is { } w && w.Pending is not (0, 0, 0, 0)
            && !await Ask("Leave the world", $"{(w.IsLive ? "Not applied" : "Unsaved")}: {Describe(w.Pending)}. Leave the world without {(w.IsLive ? "applying" : "saving")} them?", "Leave anyway", "Stay"))
        {
            return;
        }
        // An area still loading in it is not opened, nor the one left open again.
        _navigation++;
        World?.Dispose();
        World = null;
        Session = null;
        // A backup read for the Area tool's restore is not kept for the next world.
        AreaPanel.ForgetBackup();
        Tunnel.Close();
        ShowStart();
    }
    /// <summary>
    /// Says something in the status bar and, away from the 3D editor (its status bar not shown), in a dialog too, not
    /// waited for.
    /// </summary>
    /// <param name="text">What to say.</param>
    private void Say(string text)
    {
        MessageText.Text = text;
        if (_pages.Content != _editorPage)
        {
            _ = Tell(text);
        }
    }

    /// <summary>
    /// An error no part of the window caught (App): said in the status bar, so the editor stays open with the changes
    /// not saved yet.
    /// </summary>
    internal void ShowError(Exception ex)
    {
        Busy(null);
        string text = $"Something went wrong: {ex.Message} Your changes are still here. Details in the log (Open log, on the start page).";
        MessageText.Text = text;
        // Away from the 3D editor (its status bar not shown): once in a dialog, not one per error.
        if (_pages.Content != _editorPage && !_errorShown)
        {
            _errorShown = true;
            _ = Tell(text).ContinueWith(_ => _errorShown = false, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
    /// <summary>
    /// A dialog about an error is open: more errors go only to the status bar.
    /// </summary>
    private bool _errorShown;
    /// <summary>
    /// The Inspector panel: one object's data.
    /// </summary>
    internal InspectorPanel Inspector { get; }
    /// <summary>
    /// The Blueprints panel.
    /// </summary>
    internal BlueprintsPanel Blueprints { get; }
    /// <summary>
    /// The View panel's card.
    /// </summary>
    internal Control ViewPanelCard { get; } = null!;
    /// <summary>
    /// What is waiting to be saved, in the save bar.
    /// </summary>
    internal TextBlock PendingText { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Text = "All saved" };
    /// <summary>
    /// The status bar's last message.
    /// </summary>
    internal TextBlock MessageText { get; } = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>
    /// Asks before writing into the world (replaced by tests).
    /// </summary>
    internal Func<string, Task<bool>> ConfirmSave { get; set; }
    /// <summary>
    /// The question asked before saving into the world files, with what to keep in mind.
    /// </summary>
    /// <param name="what">What would be written, in words.</param>
    /// <param name="directory">The world's folder.</param>
    private static string SaveConfirmText(string what, string directory) =>
        $"Write {what} into the world files?\n\nWorld folder: {Ui.Tilde(directory)}\n\n"
        + "• Like Apply live: the world stays open with its history; undo a step and save again to take it back. The save it was opened from is kept until you leave the world.\n"
        + "• Valheim (server or game) must NOT be running with this world, or it will overwrite these changes when it saves.\n"
        + "• Test on a copy first: open it as a local world, or upload it to a test server.";
    /// <summary>
    /// A yes / no question (title, text, yes, no): Discard, Leave, Quit with unsaved changes. Tests answer it.
    /// </summary>
    internal Func<string, string, string, string, Task<bool>> Ask { get; set; }
    /// <summary>
    /// Tells the user something in a dialog titled with the app's name, as the map's are (tests replace it).
    /// </summary>
    internal Func<string, Task> Tell { get; set; }
    /// <summary>
    /// The area being edited, or null when none is open.
    /// </summary>
    internal EditSession? Session { get; private set; }
    /// <summary>
    /// Close without asking about what is not saved.
    /// </summary>
    private bool _closeAnyway;

    /// <summary>
    /// The test driver's quit: what is pending is dropped without asking.
    /// </summary>
    internal void CloseWithoutAsking()
    {
        _closeAnyway = true;
        Close();
    }

    /// <summary>
    /// Starts editing a loaded scene (also used by tests with a scene of their own).
    /// </summary>
    internal void Edit(EditSession session)
    {
        // Another area: building in the last one's dungeon (and its cut) ends, and its placed dungeon is no more to go to.
        if (BuildingInDungeon)
        {
            BuildInDungeon(false);
        }
        LastPlaced = null;
        Session = session;
        session.Brush = Tools.Brush;
        MountainPanel.FitTo(session.Ground.W);
        if (MountainPanel.Spec.Radius * 1.15f > (session.Ground.W - 1) / 2f - 2)
        {
            MountainPanel.Randomize();
        }
        session.Ground.NoLimit = Tools.NoLimitBox.IsChecked == true;
        if (session.Scene.World is { } bw)
        {
            // The builder chosen for this world before, else the world's main builder.
            TerrainEditor.Save.WorldSave.Builder = PlacePanel.Memory.Builders.TryGetValue(bw.Name, out long known) ? known : Builders.Default(bw);
            FillBuilders();
        }
        session.Mask = Mask;
        // New objects keep a green marker until saved, or until the game has them (live).
        View.InGame = id => session.Scene.Owner is { } owner && (owner.IsLive ? owner.LiveSync.IsLive(id) : owner.IsSaved(id));
        // What is placed stays visible: its kinds switched off in View are switched on.
        session.ThingsAdded += indices => Dispatcher.UIThread.Post(() => ShowKindsOf(session.Scene, indices));
        session.Changed += () => Dispatcher.UIThread.Post(() =>
        {
            QueueSupport();
            UpdateSaveBar();
            History.Refresh();
            // The dungeon's rooms changed (an edit, undo): the Dungeon tool's list follows.
            if (Tools.Mode == ToolMode.Dungeon)
            {
                DungeonPanel.Reload();
            }
            // The ground changed: the Area tool's cut and fill follows.
            if (Tools.Mode == ToolMode.Area)
            {
                AreaPanel.RefreshVolume();
            }
            // And the tape's lowest and highest ground.
            if (Tools.Mode == ToolMode.Measure)
            {
                MeasurePanel.Refresh();
            }
        });
        session.EditMade += () => Dispatcher.UIThread.Post(() => AutoApply(session));
        // Saved: the objects were read again, with new indices.
        session.ThingsReset += () => Dispatcher.UIThread.Post(() => { if (Inspector.IsOpen) { Inspector.Close(); } });
        // The kinds the Select tool can replace with.
        var kinds = session.Scene.World?.Creatable.Where(p => NameOfPrefab(p) != null).OrderBy(NameOfPrefab, StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        SelectPanel.ReplaceKinds = kinds;
        SelectPanel.ReplaceBox.ItemsSource = kinds.Select(p => NameOfPrefab(p)!).ToList();
        SelectPanel.FillSaved();
        UpdateSaveBar();
    }

    /// <summary>The save bar's pill while something waits: amber background, border and text.</summary>
    private static readonly IImmutableSolidColorBrush PillWaiting = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#2c2416")),
        PillWaitingBorder = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#7a5a2a")), PillWaitingText = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#f3d29b"));

    /// <summary>The save bar's pill: amber while something waits, grey when all is saved.</summary>
    private void SetPendingPill(bool waiting)
    {
        _pendingPill.Background = waiting ? PillWaiting : Brushes.Transparent;
        _pendingPill.BorderBrush = waiting ? PillWaitingBorder : Ui.Line;
        PendingText.Foreground = waiting ? PillWaitingText : Ui.Muted;
    }

    /// <summary>
    /// The save bar and the editor's world card as the session now stands (in the Workshop: its own save bar).
    /// </summary>
    private void UpdateSaveBar()
    {
        var s = Session;
        if (s == null)
        {
            return;
        }
        if (InWorkshop)
        {
            WorkshopSaveBar(s);
            return;
        }
        PendingText.Text = s.PendingText;
        var (z, d, a, r) = s.Pending;
        bool dirty = z + d + a + r > 0;
        SaveButton.IsEnabled = dirty;
        SaveButton.Content = s.IsLive ? "Apply live" : "Save to world";
        SaveButton.Tip(s.IsLive ? "top.apply" : "top.save");
        SetPendingPill(dirty);
        _liveBadge.IsVisible = s.IsLive;
        UndoButton.IsEnabled = s.CanUndo;
        RedoButton.IsEnabled = s.CanRedo;
        ToolTip.SetTip(UndoButton, s.UndoLabel is string u ? Lang.T("top.undoNamed", u) : Lang.T("top.undoNone"));
        ToolTip.SetTip(RedoButton, s.RedoLabel is string rl ? Lang.T("top.redoNamed", rl) : Lang.T("top.redoNone"));
        UpdateEditorWorld(s, dirty);
    }

    /// <summary>
    /// Ctrl+Z: takes the last step back (asking first for a step from an earlier session).
    /// </summary>
    internal async Task Undo()
    {
        View.SelectTool.Commit();
        if (Session is { } s && s.UndoList.Count > 0 && await AllowEarlier([s.UndoList[^1]]))
        {
            s.Undo();
        }
        UpdateSaveBar();
    }

    /// <summary>
    /// Ctrl+Shift+Z: does the last step taken back again (asking first for a step from an earlier session).
    /// </summary>
    internal async Task Redo()
    {
        View.SelectTool.Commit();
        if (Session is { } s && s.RedoList.Count > 0 && await AllowEarlier([s.RedoList[^1]]))
        {
            s.Redo();
        }
        UpdateSaveBar();
    }

    /// <summary>
    /// Steps from an earlier session of the editor (kept on disk), when the world may have changed since (live, or
    /// saved again by the game): asked once per open world before undoing or redoing them.
    /// </summary>
    /// <returns>Whether to go ahead.</returns>
    internal async Task<bool> AllowEarlier(IEnumerable<EditSession.Change> steps)
    {
        if (Session?.Scene.Owner is not { EarlierMayDiffer: true } w || !steps.Any(c => c.Earlier))
        {
            return true;
        }
        string since = w.IsLive ? "Players may have changed the world since." : "The world was saved again since (by the game, or another program).";
        string question = w.Restored!.Replayed
            ? Lang.T("history.replayedAsk", w.Restored.SavedAt.ToString("g", System.Globalization.CultureInfo.CurrentCulture))
            : $"This step was made in an earlier session of the editor (kept {w.Restored.SavedAt:g}). {since} Undoing or redoing it puts the ground and the objects back as they were then, over what changed since.";
        if (!await Ask("A step from an earlier session", question, "Go ahead", "Cancel"))
        {
            MessageText.Text = "Nothing changed.";
            return false;
        }
        w.EarlierAllowed = true;
        return true;
    }

    /// <summary>
    /// Like the web editor's Save: asks first, writes (with a backup), then says what was done. Live, it applies
    /// instead; in the Workshop it saves the blueprint.
    /// </summary>
    internal async Task Save()
    {
        var s = Session;
        // Not while saving (or reading the world): the veil is up.
        if (s == null || IsBusy)
        {
            return;
        }
        if (InWorkshop)
        {
            await SaveWorkshop();
            return;
        }
        View.SelectTool.Commit();
        var (z, d, a, r) = s.Pending;
        if (z + d + a + r == 0)
        {
            await Tell("There are no unsaved changes.");
            return;
        }
        if (s.IsLive)
        {
            await ApplyNow();
            return;
        }
        string what = Describe(s.Pending);
        if (!await ConfirmSave(SaveConfirmText(what, s.Scene.World.Directory)))
        {
            return;
        }
        SaveButton.IsEnabled = false;
        MessageText.Text = "Saving…";
        // The veil keeps the keys (Ctrl+S, Ctrl+Z), the mouse and Follow off the area while it is written.
        Busy("Saving…");
        int? newer = null;
        try
        {
            var res = await Task.Run(s.Save);
            string msg = res.Message;
            if (res.Skipped.Count > 0)
            {
                msg += "\n\nNot saved:\n• " + string.Join("\n• ", res.Skipped);
            }
            MessageText.Text = res.Message;
            Options.Say("Save: " + res.Message);
            newer = res.Saved ? null : s.Scene.Owner?.GameSaveSince();
            if (newer == null)
            {
                await Tell(msg);
            }
        }
        catch (Exception ex)
        {
            MessageText.Text = "Could not save: " + ex.Message;
        }
        finally
        {
            // Down only once the result has been read: until then Follow and the keys still wait.
            Busy(null);
        }
        // The game saved the world since: the changes can go onto its save.
        if (newer is int game && s.Scene.Owner is { } owner)
        {
            await OfferReplay(owner, game, s.Scene);
        }
        // Saved objects lose their green marks; after a failure the marks and the save bar show what is still unsaved
        // (and the Save button comes back).
        View.RefreshNewMarkers();
        UpdateSaveBar();
    }

    /// <summary>
    /// A prefab's name for people (its model's, else the catalogue's); null when unknown.
    /// </summary>
    internal string? NameOfPrefab(int prefab) => _models?.NameOf(prefab) ?? Terrain.PrefabCatalog.DisplayName(prefab);

    /// <summary>
    /// Model boxes by kind name, worked out once (null: no model).
    /// </summary>
    private readonly Dictionary<string, (System.Numerics.Vector3, System.Numerics.Vector3)?> _boxes = [];
    /// <summary>
    /// A kind's model box in its own frame, in Unity's axes (the models are stored with z mirrored) and scaled like the
    /// model; null when it has no model (or the game's models are not open).
    /// </summary>
    private (System.Numerics.Vector3 Min, System.Numerics.Vector3 Max)? ModelBox(string name)
    {
        if (_boxes.TryGetValue(name, out var known))
        {
            return known;
        }
        if (_models?.LoadModel(name) is not { } model)
        {
            return _boxes[name] = null;
        }
        System.Numerics.Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
        foreach (var part in model.Parts)
        {
            if (_models.LoadMesh(part.Mesh) is { } md)
            {
                var (a, b) = Picking.Transform(md.Bounds.Min, md.Bounds.Max, part.Matrix);
                lo = System.Numerics.Vector3.Min(lo, a);
                hi = System.Numerics.Vector3.Max(hi, b);
            }
        }
        if (lo.X > hi.X)
        {
            return _boxes[name] = null;
        }
        var sc = model.RootScale;
        return _boxes[name] = (new System.Numerics.Vector3(lo.X * sc.X, lo.Y * sc.Y, -hi.Z * sc.Z), new System.Numerics.Vector3(hi.X * sc.X, hi.Y * sc.Y, -lo.Z * sc.Z));
    }

    /// <summary>
    /// Ctrl+C: the Area selection (ground and shown objects), or the Select tool's objects.
    /// </summary>
    internal void Copy()
    {
        if (CopyToClipboard() is { } clip)
        {
            MessageText.Text = Tools.Mode == ToolMode.Select ? $"Copied {clip.Objects.Count} object(s). Ctrl+V to paste (R turns, F mirrors)."
                : $"Copied {clip.W} × {clip.H} m and {clip.Objects.Count} object(s). Ctrl+V to paste, here or in another area.";
        }
    }

    /// <summary>
    /// Ctrl+X: <see cref="Copy"/>, then the objects copied leave the world (one undo step), to be pasted elsewhere.
    /// The ground stays as it is: a cut moves what was built, not the land under it.
    /// </summary>
    internal void Cut()
    {
        if (Session is not { } s || CopyToClipboard() is not { } clip)
        {
            return;
        }
        // An area with nothing in it copies its ground only: nothing to take away, no undo step.
        if (clip.Taken.Count == 0)
        {
            MessageText.Text = $"Copied {clip.W} × {clip.H} m of ground: no objects to cut here. Ctrl+V to paste it, here or in another area.";
            return;
        }
        s.Delete(clip.Taken);
        if (Tools.Mode == ToolMode.Select)
        {
            View.Select([]);
        }
        MessageText.Text = $"Cut {clip.Objects.Count} object(s): gone from here (Ctrl+Z brings them back). Ctrl+V to paste them, here or in another area"
            + (Tools.Mode == ToolMode.Select ? " (R turns, F mirrors)." : "; the ground stays as it is.");
        UpdateSaveBar();
    }

    /// <summary>
    /// Puts the Area selection (ground and shown objects) or the Select tool's objects on the clipboard.
    /// </summary>
    /// <returns>The copy, or null (the message says what was missing).</returns>
    private CopyData? CopyToClipboard()
    {
        if (Session is not { } s)
        {
            return null;
        }
        CopyData? clip;
        if (Tools.Mode == ToolMode.Select)
        {
            View.SelectTool.Commit();
            clip = CopyData.FromThings(s.Scene, [.. View.Selected], NameOfPrefab, View.GroundHeight);
            if (clip == null)
            {
                MessageText.Text = "Select objects to copy first.";
                return null;
            }
        }
        else
        {
            var poly = View.Area.Polygon();
            var inside = poly == null ? [] : AreaPanel.ThingsInside(poly, chosenKindsOnly: false);
            clip = CopyData.FromArea(View.Area, s.Ground, s.Scene, inside, NameOfPrefab);
            if (clip == null)
            {
                MessageText.Text = "Select an area first.";
                return null;
            }
            // No objects and so no ground (it goes with buildings only): nothing a paste would put down. The clipboard
            // keeps what it had.
            if (clip.Objects.Count == 0 && Array.TrueForAll(clip.Rel, float.IsNaN))
            {
                MessageText.Text = "Nothing to copy here: the area holds no objects (its ground goes only with buildings).";
                return null;
            }
        }
        View.Paste.Clip = clip;
        View.Paste.Notify();
        return clip;
    }

    /// <summary>
    /// Ctrl+V: the Paste tool.
    /// </summary>
    internal void StartPaste()
    {
        if (View.Paste.Clip == null)
        {
            MessageText.Text = "Copy an area first (Area tool, Ctrl+C).";
            return;
        }
        View.SelectTool.Commit();
        Tools.ChooseMode(ToolMode.Paste);
        PastePanel.Refresh();
    }

    /// <summary>
    /// A click in the Paste tool: one paste (with its repeats) is one undo step.
    /// </summary>
    /// <param name="at">The grid point clicked.</param>
    internal void PasteAt(System.Numerics.Vector2 at)
    {
        if (Session is not { } s || View.Paste.Clip == null)
        {
            return;
        }
        // A dungeon blueprint: its entrance where the click is, the pieces 5000 m above.
        if (View.Paste.Clip.Dungeon)
        {
            PasteDungeon(View.Paste.Clip, at);
            return;
        }
        // Past the open area the ground could not be shaped nor the site cleared: the building would stand
        // on whatever is there, among the trees.
        if (!View.Paste.Fits(at, s.Ground.W, s.Ground.H))
        {
            MessageText.Text = "Not pasted: it goes past the open area. Open a bigger area around the place (the map), then paste again.";
            return;
        }
        // The objects' heights come from the ground as the paste leaves it: the ground step fills the list,
        // which Commit reads after it (one undo step for both).
        var add = new List<(NewObject, bool)>();
        // Clear the site: the trees, rocks and the like where the pasted building stands go too.
        var cleared = new List<int>();
        int touched = 0;
        var paste = View.Paste;
        string label = paste.Count > 1 ? $"Paste ×{paste.Count}" : "Paste";
        float ox = s.Scene.OriginX, oz = s.Scene.OriginZ;
        var linkIds = s.ReadLinks().Ids;
        s.Commit(label, g =>
        {
            var (t, rect, a) = paste.Apply(g, at, s.MaskNow(), linkIds);
            add.AddRange(a);
            touched = t.Count;
            if (paste.Site.Count > 0)
            {
                lock (s.Scene.Things)
                {
                    for (int i = 0; i < s.Scene.Things.Count; i++)
                    {
                        var th = s.Scene.Things[i];
                        if (!th.Gone && EditSession.Natural(th) && paste.Site.Contains(((int)MathF.Round(th.Position.X - ox), (int)MathF.Round(th.Position.Z - oz))))
                        {
                            cleared.Add(i);
                        }
                    }
                }
            }
            return (t, rect);
        }, cleared, add);
        MessageText.Text = $"Pasted{(paste.Count > 1 ? $" {paste.Count} copies" : "")}{(touched > 0 ? " with the ground" : "")}{(add.Count > 0 ? $", {add.Count} object(s)" : "")}"
            + $"{(cleared.Count > 0 ? $", {cleared.Count} tree(s), rock(s) and the like cleared from the site" : "")}. Click again to paste more, Esc when done.";
        UpdateSaveBar();
    }

    /// <summary>
    /// Built by: the world's builders, this computer's characters, named players, another id, or nobody.
    /// </summary>
    private void FillBuilders()
    {
        if (Session?.Scene.World is not { } w)
        {
            return;
        }
        _fillingBuilders = true;
        var players = Builders.Players(w);
        BuilderIds.Clear();
        BuilderIds.AddRange(players.Select(p => p.Id));
        BuilderIds.Add(-1);
        BuilderIds.Add(0);
        BuilderBox.ItemsSource = players.Select(p => p.Label).Append("Other player id…").Append("Nobody (not player built)").ToList();
        BuilderBox.SelectedIndex = BuilderIds.IndexOf(TerrainEditor.Save.WorldSave.Builder);
        _fillingBuilders = false;
    }

    /// <summary>
    /// Asks for a player id ("Other player id…"; replaced by tests).
    /// </summary>
    internal Func<Task<string?>> AskPlayerId { get; set; }

    /// <summary>
    /// A Built by choice: new pieces get that builder, remembered for the world ("Other player id…" asks for one).
    /// </summary>
    private async Task BuilderChosen()
    {
        if (_fillingBuilders || BuilderBox.SelectedIndex < 0 || Session?.Scene.World is not { } w)
        {
            return;
        }
        long id = BuilderIds[BuilderBox.SelectedIndex];
        if (id == -1)
        {
            string? text = (await AskPlayerId())?.Trim();
            if (!long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out id) || id == 0)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    MessageText.Text = "A player id is a whole number other than 0.";
                }
                FillBuilders();
                return;
            }
        }
        TerrainEditor.Save.WorldSave.Builder = id;
        PlacePanel.Memory.Builders[w.Name] = id;
        PlacePanel.Memory.Save();
        FillBuilders();
        MessageText.Text = id == 0 ? "New pieces get no builder: the game takes them for parts of a ruin."
            : $"New pieces are built by {Builders.Players(w).FirstOrDefault(p => p.Id == id)?.Label ?? $"player {id}"}.";
    }

    /// <summary>
    /// Make player built: the selected pieces players build that have no builder get the chosen one.
    /// </summary>
    internal void Claim()
    {
        View.SelectTool.Commit();
        if (Session is not { Scene.World: { } world } s)
        {
            return;
        }
        if (TerrainEditor.Save.WorldSave.Builder == 0)
        {
            MessageText.Text = "Built by is set to Nobody: choose a player first (View panel, Building).";
            return;
        }
        var sel = View.Selected.ToList();
        var remove = new List<int>();
        var adds = new List<(NewObject, bool)>();
        foreach (int i in sel)
        {
            var t = s.Scene.Things[i];
            if (ObjectData.Bytes(world, s.Edits, t.Id) is { } bytes && Builders.Claimed(bytes, TerrainEditor.Save.WorldSave.Builder) is { } z)
            {
                remove.Add(i);
                adds.Add((new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize()), true));
            }
        }
        if (adds.Count == 0)
        {
            MessageText.Text = "Nothing to change: the selected pieces already have a builder, or are not things players build.";
            return;
        }
        var copies = s.Commit($"Made {adds.Count} piece(s) player built", null, remove, adds);
        View.Select(sel.Where(i => !remove.Contains(i)).Concat(copies));
        MessageText.Text = $"{adds.Count} piece(s) are now player built ({sel.Count - adds.Count} left as they were). Save writes it.";
        UpdateSaveBar();
    }

    /// <summary>
    /// The inspector on the one selected object (the View panel makes room for it).
    /// </summary>
    internal void Inspect()
    {
        View.SelectTool.Commit();
        if (View.Selected.Count == 1 && Session?.Scene.Things[View.Selected.First()].Runestone == true)
        {
            MessageText.Text = "A runestone holds no data of its own: the game builds it from its location. It can only be deleted.";
            return;
        }
        if (View.Selected.Count == 1 && Inspector.Open(View.Selected.First()))
        {
            ViewPanelCard.IsVisible = false;
        }
    }

    /// <summary>
    /// The Path tool's Apply: the action along the line, in one undo step. The line is kept.
    /// </summary>
    internal void ApplyPath()
    {
        if (Session is not { } s)
        {
            return;
        }
        var path = View.Path;
        if (path.Points.Count < 2)
        {
            MessageText.Text = "Draw a line with at least two points first.";
            return;
        }
        bool clamped = false;
        if (path.Act == PathTool.Action.Cave)
        {
            MessageText.Text = ApplyCave(s, path, s.Brush, s.MaskNow());
            PathPanel.Refresh();
            UpdateSaveBar();
            return;
        }
        var touched = s.EditGround($"Path: {PathTool.Label(path.Act)}", g =>
        {
            var (t, rect, c) = path.Apply(g, s.Brush, s.Scene.Water, s.MaskNow());
            clamped = c;
            return (t, rect);
        });
        float total = PathTool.Length(path.Curve());
        MessageText.Text = touched.Count == 0 ? "Nothing changed along the line."
            : clamped ? "Applied, but part of the path reached the game limit of ±8 m from the original ground (red points)."
            : $"Applied “{PathTool.Label(path.Act)}” along {total:0} m. The line is kept, so you can apply another action (Clear to start over).";
        PathPanel.Refresh();
        UpdateSaveBar();
    }

    /// <summary>
    /// The Cave action: the trench, the trees and rocks in it taken away and the roof's boulders, in one undo step.
    /// </summary>
    /// <param name="s">The session the cave goes into, one undo step.</param>
    /// <param name="path">The line, its width, soft edge, depth, headroom, rock and seed.</param>
    /// <param name="brush">The brush whose noise roughens a natural line.</param>
    /// <param name="mask">Which ground may change (null: all of it).</param>
    /// <param name="label">The history's name for the step; null: "Cave along" and its length.</param>
    /// <returns>What was done, for the status line.</returns>
    internal static string ApplyCave(EditSession s, PathTool path, Brush brush, Func<int, float>? mask, string? label = null)
    {
        var scene = s.Scene;
        var cave = path.PlanCave(s.Ground);
        float ox = scene.OriginX, oz = scene.OriginZ, water = scene.Water;
        PathTool.CaveRock RockAt(float x, float z)
        {
            if (path.Rock != PathTool.CaveRock.Auto)
            {
                return path.Rock;
            }
            var biome = scene.Terrain?.BiomeAt(x, z);
            return biome switch
            {
                ValheimGen.Heightmap.Biome.Plains => PathTool.CaveRock.Heath,
                ValheimGen.Heightmap.Biome.Mountain or ValheimGen.Heightmap.Biome.DeepNorth => PathTool.CaveRock.Mountain,
                ValheimGen.Heightmap.Biome.Ocean => PathTool.CaveRock.Coast,
                _ => PathTool.CaveRock.Forest,
            };
        }
        var built = CaveRoof.Build(path, cave, s.Ground.W, s.Ground.H, ox, oz, RockAt);
        var roof = built.Rocks
            .Select(r => (r.Prefab, Hash: TerrainEditor.Save.StableHash.Of(r.Prefab), r.Position, r.Rotation, r.Scale))
            .Where(r => scene.World?.CanCreate(r.Hash) != false)
            .Select(r => (new NewObject(0, r.Hash, r.Position, r.Rotation, r.Scale), false)).ToList();
        // Natural objects in the trench (deeper than half a metre there) would float or stand in the way.
        var inTrench = PathTool.NearDeep(cave, path.Width / 2 + path.Soft);
        var remove = new List<int>();
        lock (scene.Things)
        {
            for (int i = 0; i < scene.Things.Count; i++)
            {
                var t = scene.Things[i];
                if (!t.Gone && EditSession.Natural(t) && inTrench(new System.Numerics.Vector2(t.Position.X - ox, t.Position.Z - oz)))
                {
                    remove.Add(i);
                }
            }
        }
        bool clamped = false;
        int open = 0;
        s.Commit(label ?? $"Cave along {PathTool.Length(cave.Curve):0} m", g =>
        {
            var (t, rect, c) = path.Apply(g, brush, water, mask, cave);
            clamped = c;
            // The walls up into the rock where its underside is above them (no daylight at the sides).
            foreach (var (p, top) in built.Seal)
            {
                if (!g.Locked(p % g.W, p / g.W) && g.HeightOf(p) < top)
                {
                    g.SetHeight(p, top);
                    t.Add(p);
                    if (g.HeightOf(p) < top - 0.5f)
                    {
                        open++;
                    }
                }
            }
            return (t, (rect.X0 - 4, rect.Z0 - 4, rect.X1 + 4, rect.Z1 + 4));
        }, remove, roof);
        return built.Uncovered > 0 && roof.Count > 0
            ? $"Dug a cave roofed with {roof.Count} boulder(s), but {built.Uncovered} point(s) of its floor stay open to the sky (at the area's edge?)."
            : roof.Count == 0
            ? "Dug the cave's trench, but it is nowhere deep enough for a roof: make Depth larger than Headroom by 1.5 m, or the line longer (its ends slope up to the ground)."
            : $"Dug a cave {PathTool.Length(cave.Curve):0} m long, roofed with {roof.Count} boulder(s){(remove.Count > 0 ? $"; {remove.Count} tree(s) and rock(s) in the way taken away" : "")}."
                + (open > 0 ? $" At {open} point(s) the walls could not rise to the rock (the game's ±8 m: No limit in the brush options lets them)." : "")
                + (clamped ? " Part of it reached the game's ±8 m limit (switch on No limit in the brush options to dig deeper)." : " Ctrl+Z takes it back.");
    }

    /// <summary>
    /// Stamp once (Raise or Lower with a stamp): the whole picture into the ground, one undo step.
    /// </summary>
    /// <param name="gx">Grid point, east.</param>
    /// <param name="gz">Grid point, north.</param>
    internal void StampAt(float gx, float gz)
    {
        if (Session is not { } s)
        {
            return;
        }
        var b = Tools.Brush;
        float amount = b.StampHeight * (Tools.Tool == BrushTool.Lower ? -1 : 1);
        bool clamped = false;
        s.EditGround($"Stamp: {b.StampLabel}", g =>
        {
            var (t, rect, c) = Sculpt.StampOnce(g, b, gx, gz, amount, s.MaskNow());
            clamped = c;
            return (t, rect);
        });
        MessageText.Text = clamped ? "Stamped, but part of it reached the game limit of ±8 m from the original ground (red points)."
            : $"Stamped {MathF.Abs(amount)} m {(amount >= 0 ? "up" : "down")}. Ctrl+Z undoes it.";
        UpdateSaveBar();
    }

    /// <summary>
    /// The Script tool's Run: the panel's script on the open area (RunScriptCode), its output below it.
    /// </summary>
    internal async Task RunScript()
    {
        if (Session == null)
        {
            MessageText.Text = "Open an area first: a script works on the open area.";
            return;
        }
        if (_stopScript != null)
        {
            return;
        }
        var panel = ScriptPanel;
        string name = panel.ScriptBox.SelectedItem as string ?? "script";
        panel.Running(true);
        try
        {
            var r = await RunScriptCode(panel.CodeBox.Text ?? "", $"Script: {name.Replace("Example: ", "", StringComparison.Ordinal)}", t => panel.Output.Text = t);
            panel.Output.Text = r.Output;
            MessageText.Text = r.Message;
        }
        finally
        {
            panel.Running(false);
        }
    }

    /// <summary>
    /// What a script gave.
    /// </summary>
    /// <param name="Changed">Whether its changes went in.</param>
    /// <param name="Output">What it printed and what became of it.</param>
    /// <param name="Message">The status bar's line.</param>
    internal sealed record ScriptOutcome(bool Changed, string Output, string Message);

    /// <summary>
    /// A script on the open area (the Script tool's Run, Claude's run_script): compiled in the background, run in a
    /// process of its own on a snapshot of the area (ScriptHost.RunInChild: Stop and the 2-minute limit end that
    /// process whatever the script is doing), then what it changed goes in as one undo step, nothing when it fails,
    /// is stopped, runs 2 minutes, or the area changed meanwhile.
    /// </summary>
    /// <param name="code">The script's C# source.</param>
    /// <param name="label">The undo step's name.</param>
    /// <param name="progress">Told "Compiling…", then "Running…".</param>
    /// <param name="apply">False: a dry run, only what it would change is said.</param>
    internal async Task<ScriptOutcome> RunScriptCode(string code, string label, Action<string>? progress = null, bool apply = true)
    {
        if (Session is not { } s)
        {
            return new(false, "", "Open an area first: a script works on the open area.");
        }
        if (_stopScript != null)
        {
            return new(false, "", "A script is already running: nothing changed.");
        }
        progress?.Invoke("Compiling…");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        _stopScript = cancel.Cancel;
        var snap = ScriptHost.Take(s, NameOfPrefab);
        int generation = s.Generation;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var (image, pdb, errors) = await Task.Run(() => ScriptHost.Compile(code));
            if (image == null)
            {
                return new(false, "The script has mistakes:\n" + string.Join("\n", errors), "The script has mistakes (see below it): nothing changed.");
            }
            progress?.Invoke("Running…");
            var ch = await ScriptHost.RunInChild(image, pdb, snap, cancel.Token);
            if (Session != s)
            {
                string gone = Session == null
                    ? "The area was closed while the script ran: nothing changed."
                    : "Another area was opened while the script ran: nothing changed.";
                return new(false, ch.Output + gone, gone);
            }
            // Its heights and object numbers are those of the area when it started: changed since (a
            // stroke, an undo, a save that read the world again), they would undo that or hit other objects.
            if (s.Generation != generation)
            {
                return new(false, ch.Output + "The area changed while the script ran: nothing changed. Run it again.", "The area changed while the script ran: nothing changed. Run it again.");
            }
            if (ch.Empty)
            {
                return new(false, ch.Output + "The script ran and changed nothing.", "The script ran and changed nothing.");
            }
            var parts = new List<string>();
            if (ch.Heights.Count > 0)
            {
                parts.Add($"{ch.Heights.Count:N0} ground point(s)");
            }

            if (ch.Paint.Count > 0)
            {
                parts.Add($"{ch.Paint.Count:N0} painted");
            }

            if (ch.Add.Count > 0)
            {
                parts.Add($"{ch.Add.Count:N0} object(s) placed");
            }

            int moves = ch.Moves.Keys.Count(i => !ch.Remove.Contains(i));
            if (moves > 0)
            {
                parts.Add($"{moves:N0} moved");
            }

            if (ch.Remove.Count > 0)
            {
                parts.Add($"{ch.Remove.Count:N0} taken away");
            }

            if (!apply)
            {
                string would = $"Dry run: the script would change {string.Join(", ", parts)}; nothing changed.";
                return new(false, ch.Output + would, would);
            }
            int clamped = ScriptHost.Apply(s, ch, label);
            string what = $"The script changed {string.Join(", ", parts)} in {watch.Elapsed.TotalSeconds:0.0} s; Ctrl+Z takes it all back."
                + (clamped > 0 ? $" {clamped:N0} point(s) stopped at the game's ±8 m (Ground.NoLimit = true lets them go further)." : "");
            UpdateSaveBar();
            return new(true, ch.Output + what, what);
        }
        catch (OperationCanceledException)
        {
            return new(false, "Stopped: nothing changed.", "The script was stopped (or ran 2 minutes): nothing changed.");
        }
        catch (Exception ex)
        {
            return new(false, $"The script stopped with an error, nothing changed:\n{ScriptHost.Describe(ex)}", "The script stopped with an error (see below it): nothing changed.");
        }
        finally
        {
            _stopScript = null;
        }
    }

    /// <summary>
    /// The Mountain tool's click: the mountain goes into the ground there (grid point), then the biome's trees and
    /// rocks grow on it (a second undo step).
    /// </summary>
    internal async Task PutMountain(float gx, float gz)
    {
        if (Session is not { } s)
        {
            return;
        }
        var m = MountainPanel.Spec;
        string name = MountainPanel.Preset.Name;
        if (s.Mountain(gx, gz, m, MountainPanel.Clear, $"Mountain: {name}") is not { } done)
        {
            MessageText.Text = $"The {name.ToLowerInvariant()} reaches {m.Reach:0} m around the click and does not fit in the open area: click nearer its middle, make it smaller, or open a bigger area.";
            return;
        }
        UpdateSaveBar();
        string placed = $"Placed a {name.ToLowerInvariant()}, {m.Height:0} m high{(done.Cleared > 0 ? $"; {done.Cleared} tree(s) and rock(s) it buried taken away" : "")}.";
        MessageText.Text = placed;
        if (!MountainPanel.Grow)
        {
            MessageText.Text = placed + " Ctrl+Z takes it back; saving turns it into ground discs.";
            return;
        }
        var shape = Mountain.Shape(m);
        float ox = s.Scene.OriginX, oz = s.Scene.OriginZ, wx = ox + gx, wz = oz + gz;
        // The spots are judged on the area as it is now: what grows is only put in if it is still the same.
        int generation = s.Generation;
        // On the slopes it raised by more than a metre (not around its foot, where the old ones stand).
        var spots = await Growth.Spots(s, wx - m.Reach, wz - m.Reach, wx + m.Reach, wz + m.Reach, o => shape(o.X - wx, o.Z - wz) > 1,
            NameOfPrefab, t => MessageText.Text = placed + " " + t, maxZones: 49);
        if (Session != s || s.Generation != generation)
        {
            // Another area opened (Follow grew it), or the ground changed (an undo, a stroke, another mountain): the
            // trees would go to an area no longer shown, or float or be buried.
            MessageText.Text = placed + " Nothing grew on it: the area changed while its trees were worked out.";
            UpdateSaveBar();
            return;
        }
        if (spots is { Count: > 0 })
        {
            Growth.Commit(s, $"Mountain: grew {spots.Count} object(s)", spots);
            placed += $" Grew {spots.Count} of the biome's trees and rocks on it.";
            MessageText.Text = placed + " Ctrl+Z takes it back (twice with what grew); saving turns it into ground discs.";
        }
        else
        {
            // Nothing grew: one step to take back. When growing could not be done, why (Spots said it) stays.
            MessageText.Text = (spots == null ? MessageText.Text : placed) + " Ctrl+Z takes it back; saving turns it into ground discs.";
        }
        UpdateSaveBar();
    }

    /// <summary>
    /// The Shape tool's click: the shape goes into the ground there (grid point).
    /// </summary>
    internal void PutShape(float gx, float gz)
    {
        if (Session is not { } s)
        {
            return;
        }
        if (ShapePanel.Function is not { } f)
        {
            MessageText.Text = "Fix the formula first.";
            return;
        }
        var (touched, clamped, bad) = s.Shape(gx, gz, f, ShapePanel.Radius, ShapePanel.Height, ShapePanel.Label);
        MessageText.Text = touched == 0 ? "The formula gives 0 everywhere within the radius: nothing changed." + (bad > 0 ? $" ({bad} point(s) gave no number.)" : "")
            : clamped ? "Placed, but part of the shape reached the game limit of ±8 m from the original ground (red points)."
            : $"Placed the shape on {touched} point(s). Ctrl+Z undoes it.";
        UpdateSaveBar();
    }

    /// <summary>
    /// The driver: the right-hand panel by name (view, history, help or none).
    /// </summary>
    internal void ShowRightPanel(string name) => ShowRight(name switch { "view" => ViewPanelCard, "history" => History.Card, "help" => HelpCard, _ => null });

    /// <summary>
    /// The right-hand panels share the place under the top bar: one at a time (null: none). The choice is remembered.
    /// </summary>
    internal void ShowRight(Control? panel)
    {
        if (panel == ViewPanelCard || panel == HelpCard || panel == History.Card || panel == null)
        {
            Prefs.Set("panel.right", panel == ViewPanelCard ? "view" : panel == HelpCard ? "help" : panel == History.Card ? "history" : "none");
        }
        ViewPanelCard.IsVisible = panel == ViewPanelCard;
        HelpCard.IsVisible = panel == HelpCard;
        if (History.Card.IsVisible != (panel == History.Card))
        {
            History.Toggle();
        }
        ViewButton.Classes.Set("on", ViewPanelCard.IsVisible);
        HistoryButton.Classes.Set("on", History.Card.IsVisible);
        HelpButton.Classes.Set("on", HelpCard.IsVisible);
    }

    /// <summary>
    /// A thin upright line between groups of the top bar.
    /// </summary>
    private static Border Sep() => new() { Width = 1, Height = 26, Background = Ui.Line, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// The top bar (the web editor's): back to the map, the names, undo and redo, what is waiting to be saved and Save,
    /// and the right-hand panels.
    /// </summary>
    private Border TopBar()
    {
        UndoButton.Click += async (_, _) => await Undo();
        MapButton.Click += async (_, _) =>
        {
            if (InWorkshop)
            {
                await LeaveWorkshop();
            }
            else
            {
                ShowMap();
            }
        };
        MapButton.Tip("top.map");
        ToolTip.SetTip(UndoButton, Lang.T("top.undo"));
        ToolTip.SetTip(RedoButton, Lang.T("top.redo"));
        HistoryButton.Click += (_, _) => ShowRight(History.Card.IsVisible ? null : History.Card);
        History.Closed += () => HistoryButton.Classes.Set("on", false);
        HistoryButton.Tip("top.history");
        ViewButton.Click += (_, _) => ShowRight(ViewPanelCard.IsVisible ? null : ViewPanelCard);
        ViewButton.Tip("top.view");
        HelpButton.Click += (_, _) => ShowRight(HelpCard.IsVisible ? null : HelpCard);
        HelpButton.Tip("top.help");
        RedoButton.Click += async (_, _) => await Redo();
        SaveButton.Click += async (_, _) => await Save();
        SaveButton.Tip("top.save");
        ToolTip.SetTip(_liveBadge, Lang.T("top.liveBadge"));
        _pendingPill.Child = PendingText;
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _title, _subtitle } };
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { MapButton, names, AreaNav } };
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { UndoButton, RedoButton, Sep(), _liveBadge, ReloadButton, AutoApplyBox, SupportBox, CutBox, _pendingPill, DiscardButton, SaveButton, Sep(), HistoryButton, ViewButton, HelpButton },
        };
        Grid.SetColumn(right, 2);
        bar.Children.Add(left);
        bar.Children.Add(right);
        var card = Ui.Card(bar);
        card.Height = 50;
        card.Padding = new Thickness(Ui.Pad.Left, 0);
        card.Margin = new Thickness(10, 10, 10, 0);
        card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }

    /// <summary>
    /// The status bar's cursor readout: where the pointer is on the ground (see CursorReadout).
    /// </summary>
    internal TextBlock CursorText { get; } = new() { FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
    /// <summary>
    /// Where the pointer last was on the 3D view (null: off it), so the readout can follow a stroke's changes.
    /// </summary>
    private Point? _cursorAt;

    /// <summary>
    /// The readout for a point of the 3D view (null: the pointer left it).
    /// </summary>
    internal void ShowCursor(Point? at)
    {
        _cursorAt = at;
        CursorText.Text = at is Point p && View.Scene is { } s && Surface is { } surface && View.GridAt(p, surface.Bounds.Size) is { } g ? CursorReadout.Text(s, g.X, g.Y) : "";
    }

    /// <summary>
    /// The status bar (bottom): the cursor, the last message, the selection, walking or flying, and the controls.
    /// </summary>
    private Border StatusBar()
    {
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), ColumnSpacing = 18 };
        var tips = new TextBlock { Text = "Right drag turns · Wheel zooms · Middle drag slides · ? help", FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center };
        _selection.VerticalAlignment = _eye.VerticalAlignment = VerticalAlignment.Center;
        _selection.TextWrapping = _eye.TextWrapping = TextWrapping.NoWrap;
        _selection.MaxWidth = 380;
        _selection.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(MessageText, 1);
        Grid.SetColumn(_selection, 2);
        Grid.SetColumn(_eye, 3);
        Grid.SetColumn(tips, 4);
        bar.Children.Add(CursorText);
        bar.Children.Add(MessageText);
        bar.Children.Add(_selection);
        bar.Children.Add(_eye);
        bar.Children.Add(tips);
        var card = Ui.Card(bar);
        card.Margin = new Thickness(10, 0, 10, 10);
        card.VerticalAlignment = VerticalAlignment.Bottom;
        return card;
    }

    /// <summary>
    /// Help: the controls (the web editor's list), then what the program knows about this computer.
    /// </summary>
    /// <param name="record">The Record frame rates switch, shown at its end.</param>
    private Border Help(CheckBox record)
    {
        var list = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, RowSpacing = 5 };
        var rows = new (string Keys, string What)[]
        {
            ("Left drag", "Use the tool"), ("Right drag", "Turn the view"), ("Wheel", "Zoom"),
            ("Middle drag", "Slide the view (also Space + left drag, Shift + right drag, the View tool)"), ("W A S D", "Move around"),
            ("F", "Walk at eye height · fly (Space up, C down) · back to the usual view"), ("1–9, 0, O", "Sculpt and paint tools (O: Erode)"),
            ("B / P / T / G", "Area, Path, Place, Shape"), ("E / M / H", "Select, Measure, View (Esc too)"), ("[  ]", "Brush size"),
            ("Ctrl+C / Ctrl+X / Ctrl+V", "Copy or cut the area or selection / paste it (R turns 90°, , . turn 1°, F mirrors)"),
            ("Alt + wheel", "Turn the selection, the paste or the Place preview (, . too; Shift: 15°)"), ("Place: R", "New layout"),
            ("Alt + click", "Pick the ground height"), ("Del", "Delete the selected objects"), ("I", "Inspect the selected object"),
            ("PgUp / PgDn / End", "Lift, lower or drop the selection"),
            ("Ctrl+Z / Ctrl+Y", "Undo / redo"), ("Ctrl+S", "Save"), ("V / L / ?", "View panel / history / this help"),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            list.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var k = new TextBlock { Text = rows[i].Keys, FontSize = 12.5 };
            var w = new TextBlock { Text = rows[i].What, FontSize = 12.5, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(k, i);
            Grid.SetRow(w, i);
            Grid.SetColumn(w, 1);
            list.Children.Add(k);
            list.Children.Add(w);
        }
        var close = new Button { Content = Icons.Make("close", 14), Padding = new Thickness(5) }.Classed("ghost").Tip("card.close");
        close.Click += (_, _) => ShowRight(null);
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "Controls", FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } };
        Grid.SetColumn(close, 1);
        head.Children.Add(close);
        record.Classes.Add("switch");
        var card = Ui.Card(new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    head, list,
                    Ui.Hint("Edits stay within the game's limit of ±8 m from the original ground; points at the limit turn red (View: Limit marks)."),
                    Ui.Hint("The outer line of points is locked so the area always joins its neighbours seamlessly."),
                    Ui.Heading("This computer"),
                    _fps, _info, record,
                },
            },
        });
        card.Width = 330;
        card.IsVisible = false;
        return card;
    }

    /// <summary>
    /// What is remembered between runs (Prefs, as the web editor kept it in the browser): each control set from it now
    /// and remembered as it changes.
    /// </summary>
    private void RememberPrefs()
    {
        var p = Prefs;
        // View: the kinds drawn, water, overlays, the look's slope colours and height lines.
        foreach (var (k, box) in _kindBoxes)
        {
            p.Bind(box, $"view.show.{k}");
        }
        p.Bind(WaterBox, "view.water");
        p.Bind(UnsavedBox, "view.unsaved");
        p.Bind(LimitBox, "view.limit");
        foreach (var (layer, box) in _overlayBoxes)
        {
            p.Bind(box, $"view.overlay.{layer}");
        }
        p.Bind(SlopeBox, "view.slope");
        p.Bind(ContourBox, "view.contour");
        p.Bind(ContourStepBox, "view.contourStep");
        // The look: the game's or plain, buildings see-through, the 3D view's resolution.
        p.Bind(GameLookBox, "view.gameLook");
        p.Bind(SeeThroughBox, "view.seeThrough");
        p.Bind(ResolutionBox, "view.res3d");
        // The brush's shape and falloff, the Area action, Select's options.
        p.Bind(Tools.ShapeBox, "brush.shape");
        p.Bind(Tools.FalloffBox, "brush.falloff");
        p.Bind(AreaPanel.ActionBox, "area.action");
        p.Bind(SelectPanel.GroundBox, "select.onGround");
        p.Bind(SelectPanel.SnapBox, "select.snap");
        // Shape: the preset, and the formula when it is one's own.
        p.Bind(ShapePanel.PresetBox, "shape.preset");
        bool Own() => ShapePanel.PresetBox.SelectedIndex == ShapePanel.Presets.Length;
        if (Own() && p.TryGet("shape.formula", out string? formula) && formula != null)
        {
            ShapePanel.FormulaBox.Text = formula;
        }
        ShapePanel.FormulaBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && Own())
            {
                p.Set("shape.formula", ShapePanel.FormulaBox.Text ?? "");
            }
        };
        // The Place tool's list: which categories are open.
        if (p.TryGet("place.openKinds", out string[]? open) && open != null)
        {
            PlacePanel.OpenKinds.Clear();
            foreach (string name in open)
            {
                if (Enum.TryParse<ObjectKind>(name, out var k))
                {
                    PlacePanel.OpenKinds.Add(k);
                }
            }
        }
        PlacePanel.OpenKindsChanged += () => p.Set("place.openKinds", PlacePanel.OpenKinds.Select(k => k.ToString()).Order().ToArray());
        // The right-hand panel open.
        ShowRight(p.Get("panel.right", "view") switch { "help" => HelpCard, "history" => History.Card, "none" => null, _ => ViewPanelCard });
        // The clipboard (its objects' ids only meant something in the world it came from).
        if (p.TryGet("clipboard", out System.Text.Json.Nodes.JsonObject? clip) && clip != null)
        {
            try
            {
                View.Paste.Clip = CopyFormat.FromJson(clip, keepSources: false);
                _keptClip = View.Paste.Clip;
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or NullReferenceException or ArgumentException or System.Text.Json.JsonException)
            {
                p.Remove("clipboard");
            }
        }
        View.Paste.Changed += () =>
        {
            if (View.Paste.Clip is { } c && !ReferenceEquals(c, _keptClip))
            {
                _keptClip = c;
                p.Set("clipboard", CopyFormat.ToJson(c));
            }
        };
    }

    /// <summary>
    /// The clipboard last remembered (or read back at start), so it is written only when it changes.
    /// </summary>
    private CopyData? _keptClip;

    /// <summary>
    /// Placing (or pasting) a kind switched off in View switches it on, so what was placed is seen; said after what the
    /// tool says about the placing, as the web editor did.
    /// </summary>
    /// <param name="scene">The scene the objects are in.</param>
    /// <param name="indices">The new objects' indices in it.</param>
    internal void ShowKindsOf(WorldScene scene, IReadOnlyList<int> indices)
    {
        var turned = new List<string>();
        foreach (var k in indices.Where(i => i < scene.Things.Count).Select(i => ObjectKinds.Of(NameOfPrefab(scene.Things[i].Prefab), scene.Things[i].Piece, scene.Things[i].Tamed)).Distinct())
        {
            if (_kindBoxes.TryGetValue(k, out var box) && box.IsChecked != true)
            {
                box.IsChecked = true;
                turned.Add(ObjectKinds.Label(k));
            }
        }
        // (The Workshop has no View panel to say it in.)
        if (turned.Count == 0 || InWorkshop)
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            string said = MessageText.Text ?? "";
            MessageText.Text = $"{(said != "" ? said + " " : "")}Switched on {string.Join(", ", turned)} in View, so what you placed stays visible.";
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Eyedropper for the Replace lists: the next click on an object gives its kind (Esc cancels).
    /// </summary>
    /// <param name="label">What the kind is for, in the message.</param>
    /// <param name="done">Given the picked object's prefab.</param>
    internal void PickKind(string label, Action<int> done)
    {
        View.PickObjectOnce = i =>
        {
            if (i is int t && View.Scene is { } sc && t < sc.Things.Count)
            {
                done(sc.Things[t].Prefab);
            }
            else
            {
                MessageText.Text = "Nothing picked: click right on an object (only things that are shown can be picked).";
            }
        };
        MessageText.Text = $"Click an object to pick its kind for {label}. Esc cancels.";
    }
    /// <summary>
    /// , . turn the paste by 1° (Shift: 15°); . is clockwise seen from above, like the other tools.
    /// </summary>
    /// <returns>Always true: the key is taken.</returns>
    private bool TurnPaste(Avalonia.Input.Key key, bool shift)
    {
        float step = shift ? 15 : 1;
        View.Paste.TurnBy(key == Avalonia.Input.Key.OemComma ? step : -step);
        return true;
    }

    /// <summary>
    /// Alt + wheel: the tool that turns things turns them, as its , and . keys do; otherwise (false) it zooms.
    /// </summary>
    /// <returns>Whether a tool took it.</returns>
    internal bool AltWheel(Avalonia.Input.Key key, bool shift) => Tools.Mode switch
    {
        ToolMode.Paste => TurnPaste(key, shift),
        ToolMode.Place => PlaceInput.Key(key, shift, false),
        _ when Tools.SelectMode => View.SelectTool.Key(key, shift),
        _ => false,
    };

    /// <summary>
    /// The 3D editor's keys (shortcuts for the tools, the panels, undo, save and the like); none while typing in a box
    /// or away from the editor.
    /// </summary>
    private void OnKey(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        // Typing in a box: its keys are its own. The keys below are the 3D editor's: on the start page or
        // the map they would act on the area left open behind it (an undo pushed live, unseen); under the busy veil
        // (saving, reading the world again) they would wait on it or do it twice.
        if (e.Source is TextBox || _pages.Content != _editorPage || IsBusy)
        {
            return;
        }
        var mods = e.KeyModifiers;
        bool ctrl = mods.HasFlag(Avalonia.Input.KeyModifiers.Control) || mods.HasFlag(Avalonia.Input.KeyModifiers.Meta);
        if (!ctrl && e.Key == Avalonia.Input.Key.Escape && View.PickObjectOnce != null)
        {
            View.PickObjectOnce = null;
            MessageText.Text = "Picking cancelled.";
            e.Handled = true;
            return;
        }
        // The right-hand panels, as in the web editor: V the View panel, L the history, ? (or F3) the help. ? is Shift
        // and the / key (OemQuestion and Oem2 are that one key): / alone is not it.
        if (!ctrl && (e.Key == Avalonia.Input.Key.F3 || mods.HasFlag(Avalonia.Input.KeyModifiers.Shift) && e.Key == Avalonia.Input.Key.OemQuestion))
        {
            ShowRight(HelpCard.IsVisible ? null : HelpCard);
            e.Handled = true;
            return;
        }
        if (!ctrl && mods == Avalonia.Input.KeyModifiers.None && e.Key == Avalonia.Input.Key.V)
        {
            ShowRight(ViewPanelCard.IsVisible ? null : ViewPanelCard);
            e.Handled = true;
            return;
        }
        if (!ctrl && mods == Avalonia.Input.KeyModifiers.None && e.Key == Avalonia.Input.Key.L)
        {
            ShowRight(History.Card.IsVisible ? null : History.Card);
            e.Handled = true;
            return;
        }
        if (ctrl && e.Key == Avalonia.Input.Key.Z)
        {
            _ = mods.HasFlag(Avalonia.Input.KeyModifiers.Shift) ? Redo() : Undo();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Avalonia.Input.Key.Y)
        {
            _ = Redo();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Avalonia.Input.Key.C && Tools.Mode is ToolMode.Area or ToolMode.Select)
        {
            Copy();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Avalonia.Input.Key.X && Tools.Mode is ToolMode.Area or ToolMode.Select)
        {
            Cut();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Avalonia.Input.Key.V)
        {
            StartPaste();
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Paste && !ctrl && e.Key is Avalonia.Input.Key.R or Avalonia.Input.Key.F or Avalonia.Input.Key.OemComma or Avalonia.Input.Key.OemPeriod or Avalonia.Input.Key.Escape)
        {
            switch (e.Key)
            {
                case Avalonia.Input.Key.R: View.Paste.TurnBy(90); break;
                case Avalonia.Input.Key.F: View.Paste.Mirror = !View.Paste.Mirror; View.Paste.Notify(); break;
                case Avalonia.Input.Key.OemComma or Avalonia.Input.Key.OemPeriod: TurnPaste(e.Key, mods.HasFlag(Avalonia.Input.KeyModifiers.Shift)); break;
                default: Tools.ChooseMode(ToolMode.Area); break;
            }
            e.Handled = true;
        }
        else if (ctrl && e.Key == Avalonia.Input.Key.S)
        {
            _ = Save();
            e.Handled = true;
        }
        else if (!ctrl && e.Key is Avalonia.Input.Key.OemOpenBrackets or Avalonia.Input.Key.OemCloseBrackets)
        {
            Tools.ResizeBrush(e.Key == Avalonia.Input.Key.OemOpenBrackets ? -1 : 1);
            e.Handled = true;
        }
        else if (!ctrl && e.Key == Avalonia.Input.Key.Escape && Inspector.IsOpen)
        {
            Inspector.Close();
            e.Handled = true;
        }
        else if (!ctrl && e.Key == Avalonia.Input.Key.I && Tools.SelectMode && View.Selected.Count == 1)
        {
            Inspect();
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Place && PlaceInput.Key(e.Key, mods.HasFlag(Avalonia.Input.KeyModifiers.Shift), ctrl))
        {
            e.Handled = true;
        }
        else if (Tools.SelectMode && View.SelectTool.Key(e.Key, mods.HasFlag(Avalonia.Input.KeyModifiers.Shift)))
        {
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Area && !ctrl && e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Return)
        {
            if (!View.Area.CloseWithEnter())
            {
                _ = AreaPanel.Apply();
            }
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Area && !ctrl && e.Key == Avalonia.Input.Key.Back)
        {
            View.Area.RemoveLast();
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Area && !ctrl && e.Key == Avalonia.Input.Key.Escape && View.Area.Points.Count > 0)
        {
            View.Area.Clear();
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Path && !ctrl && e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Return)
        {
            ApplyPath();
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Path && !ctrl && e.Key == Avalonia.Input.Key.Back)
        {
            View.Path.RemoveLast();
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Path && !ctrl && e.Key == Avalonia.Input.Key.Escape && View.Path.Points.Count > 0)
        {
            View.Path.Clear();
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Dungeon && !ctrl && e.Key == Avalonia.Input.Key.Delete && View.Dungeon.Delete())
        {
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Dungeon && !ctrl && e.Key == Avalonia.Input.Key.R)
        {
            View.Dungeon.Turn();
            e.Handled = true;
        }
        else if (Tools.Mode == ToolMode.Measure && !ctrl && e.Key == Avalonia.Input.Key.Escape && View.Tape.A != null)
        {
            View.Tape.Clear();
            e.Handled = true;
        }
        else if (!ctrl && e.Key == Avalonia.Input.Key.Escape)
        {
            Tools.Key("Escape");
        }
        else if (!ctrl && e.Key is Avalonia.Input.Key.E or Avalonia.Input.Key.M or Avalonia.Input.Key.G or Avalonia.Input.Key.P or Avalonia.Input.Key.B or Avalonia.Input.Key.O or Avalonia.Input.Key.T or Avalonia.Input.Key.H)
        {
            Tools.Key(e.Key.ToString());
        }
        else if (!ctrl && e.Key >= Avalonia.Input.Key.D0 && e.Key <= Avalonia.Input.Key.D9)
        {
            Tools.Key((e.Key - Avalonia.Input.Key.D0).ToString());
        }
        else if (!ctrl && e.Key >= Avalonia.Input.Key.NumPad0 && e.Key <= Avalonia.Input.Key.NumPad9)
        {
            Tools.Key((e.Key - Avalonia.Input.Key.NumPad0).ToString());
        }
    }

    // The View panel (top right): which kinds of objects are drawn, with how many the area has, and the water.
    /// <summary>
    /// One switch per kind of object.
    /// </summary>
    private readonly Dictionary<ObjectKind, CheckBox> _kindBoxes = [];
    /// <summary>
    /// The View panel's switch per kind of object.
    /// </summary>
    internal IReadOnlyDictionary<ObjectKind, CheckBox> KindBoxes => _kindBoxes;
    /// <summary>
    /// Draw the water.
    /// </summary>
    internal CheckBox WaterBox { get; } = new() { Content = "Water", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Mark what is not saved yet (new objects keep a green marker).
    /// </summary>
    internal CheckBox UnsavedBox { get; } = new() { Content = "Unsaved marks", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Mark ground points at the game's ±8 m limit in red.
    /// </summary>
    internal CheckBox LimitBox { get; } = new() { Content = "Limit marks", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// One switch per overlay layer.
    /// </summary>
    private readonly Dictionary<Overlays.Layer, CheckBox> _overlayBoxes = [];
    /// <summary>
    /// The View panel's switch per overlay layer.
    /// </summary>
    internal IReadOnlyDictionary<Overlays.Layer, CheckBox> OverlayBoxes => _overlayBoxes;
    /// <summary>
    /// Colour the ground by its slope.
    /// </summary>
    internal CheckBox SlopeBox { get; } = new() { Content = "Slope colours", FontSize = 12 };
    /// <summary>
    /// Building: who new pieces are built by (the player ids: the list's entries).
    /// </summary>
    internal ComboBox BuilderBox { get; } = new() { FontSize = 12, MinWidth = 200, MaxWidth = 260 };
    /// <summary>
    /// The player id of each Built by entry, in order: the players, then -1 (another id, asked for) and 0 (nobody).
    /// </summary>
    internal List<long> BuilderIds { get; } = [];
    /// <summary>
    /// True while the Built by list is filled, so it is not taken for a choice.
    /// </summary>
    private bool _fillingBuilders;
    /// <summary>
    /// Draw height lines.
    /// </summary>
    internal CheckBox ContourBox { get; } = new() { Content = "Height lines every", FontSize = 12 };
    /// <summary>
    /// How far apart the height lines are, in metres.
    /// </summary>
    internal ComboBox ContourStepBox { get; } = new() { ItemsSource = new[] { "1", "2", "5", "10" }, SelectedIndex = 1, FontSize = 12, MinWidth = 60 };

    /// <summary>
    /// A small heading of the View panel.
    /// </summary>
    private static TextBlock Heading(string t) => Ui.Heading(t, 12);

    // View, Look: the game's look or plain colours and boxes, see-through buildings, and the 3D
    // resolution (GlView). Presets switch every kind and overlay to the defaults, all on, or only the
    // ground (the web editor's buttons).
    /// <summary>
    /// The game's look, or plain colours and boxes.
    /// </summary>
    internal CheckBox GameLookBox { get; } = new() { Content = "Game look", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Draw buildings see-through.
    /// </summary>
    internal CheckBox SeeThroughBox { get; } = new() { Content = "See-through buildings", FontSize = 12 };
    /// <summary>
    /// The 3D view's resolution: the screen's, balanced, or fast.
    /// </summary>
    internal ComboBox ResolutionBox { get; } = new() { ItemsSource = new[] { "Sharp (the screen's)", "Balanced", "Fast" }, SelectedIndex = 0, FontSize = 12, MinWidth = 150 };
    /// <summary>
    /// Every kind, the water and the overlays as they start.
    /// </summary>
    internal Button DefaultsButton { get; } = new() { Content = "Defaults", FontSize = 12 };
    /// <summary>
    /// Every kind, the water and the overlays on.
    /// </summary>
    internal Button AllButton { get; } = new() { Content = "All", FontSize = 12 };
    /// <summary>
    /// Only the ground: every kind, the water and the overlays off.
    /// </summary>
    internal Button GroundButton { get; } = new() { Content = "Ground", FontSize = 12 };
    /// <summary>
    /// Each preset switch and whether it starts on (the defaults).
    /// </summary>
    private readonly Dictionary<CheckBox, bool> _viewDefaults = [];

    /// <summary>
    /// The View panel's presets.
    /// </summary>
    internal enum ViewPreset { Defaults, All, Ground }

    /// <summary>
    /// Sets every kind, the water and the overlays as the preset says.
    /// </summary>
    internal void ShowPreset(ViewPreset preset)
    {
        foreach (var (box, on) in _viewDefaults)
        {
            box.IsChecked = preset switch { ViewPreset.All => true, ViewPreset.Ground => false, _ => on };
        }
    }

    /// <summary>
    /// Builds the View panel and wires it to the 3D view.
    /// </summary>
    private Border ViewPanel()
    {
        var card = BuildViewPanel();
        WireViewPanel();
        return card;
    }

    /// <summary>
    /// Lays out the View panel: its headings, switches, lists and preset buttons with their tips; the switches'
    /// starting states are kept as the presets' defaults.
    /// </summary>
    private Border BuildViewPanel()
    {
        var list = new StackPanel { Spacing = 2 };
        list.Children.Add(Ui.Heading("View", 0));
        list.Children.Add(Heading("LOOK"));
        GameLookBox.Tip("view.gameLook");
        WaterBox.Tip("view.water");
        SlopeBox.Tip("view.slope");
        ContourBox.Tip("view.contour");
        ContourStepBox.Tip("view.contourStep");
        DefaultsButton.Tip("view.defaults");
        AllButton.Tip("view.all");
        SeeThroughBox.Tip("view.seeThrough");
        ResolutionBox.Tip("view.res3d");
        list.Children.Add(GameLookBox);
        list.Children.Add(SeeThroughBox);
        list.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "3D resolution", FontSize = 12 }, ResolutionBox } });
        list.Children.Add(Heading("OBJECTS"));
        foreach (var k in ObjectKinds.All)
        {
            var box = new CheckBox { Content = ObjectKinds.Label(k), IsChecked = View.IsShown(k), FontSize = 12 };
            ToolTip.SetTip(box, Tips.Kind(k));
            _kindBoxes[k] = box;
            list.Children.Add(box);
        }
        list.Children.Add(WaterBox);
        list.Children.Add(Heading("OVERLAYS"));
        foreach (var (layer, label) in new[] { (Overlays.Layer.Borders, "Zone borders"), (Overlays.Layer.Markers, "Location markers"), (Overlays.Layer.Wards, "Ward areas"),
            (Overlays.Layer.Stations, "Build ranges"), (Overlays.Layer.Flatten, "Location flattening") })
        {
            var box = new CheckBox { Content = label, IsChecked = View.IsOverlayShown(layer), FontSize = 12, Tag = label };
            ToolTip.SetTip(box, Tips.Overlay(layer));
            _overlayBoxes[layer] = box;
            list.Children.Add(box);
        }
        UnsavedBox.Tip("view.unsaved");
        list.Children.Add(UnsavedBox);
        LimitBox.Tip("view.limit");
        list.Children.Add(LimitBox);
        // The presets cover what is shown: kinds, water and overlays (as they are now: the defaults).
        foreach (var box in _kindBoxes.Values.Append(WaterBox).Concat(_overlayBoxes.Values))
        {
            _viewDefaults[box] = box.IsChecked == true;
        }
        GroundButton.Tip("view.ground");
        list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0), Children = { DefaultsButton, AllButton, GroundButton } });
        list.Children.Add(Heading("BUILDING"));
        BuilderBox.Tip("view.builder");
        list.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "Built by", FontSize = 12 }, BuilderBox } });
        list.Children.Add(Heading("GROUND (game look)"));
        list.Children.Add(SlopeBox);
        list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ContourBox, ContourStepBox, new TextBlock { Text = "m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } });
        list.Children.Add(PlayersControl());
        // The web editor's switches.
        foreach (var box in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(list).OfType<CheckBox>())
        {
            box.Classes.Add("switch");
            box.FontSize = 12.5;
        }
        list.Width = 240;
        return Ui.AsCard(new Border
        {
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            // Scrolls when the window is too short for it (a bar only then).
            Child = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = list },
        });
    }

    /// <summary>
    /// Wires the View panel (built first) to the 3D view: each switch and list sets what is drawn, the presets set
    /// the switches, and the view's counts go into the switches' labels.
    /// </summary>
    private void WireViewPanel()
    {
        GameLookBox.IsCheckedChanged += (_, _) => View.GameLookOn = GameLookBox.IsChecked == true;
        SeeThroughBox.IsCheckedChanged += (_, _) => View.SeeThroughBuildings = SeeThroughBox.IsChecked == true;
        ResolutionBox.SelectionChanged += (_, _) =>
        {
            View.Resolution3D = (GlView.Resolution)Math.Max(0, ResolutionBox.SelectedIndex);
            var (w, h) = View.RenderSize();
            MessageText.Text = $"3D resolution: {ResolutionBox.SelectedItem}, {w}×{h} pixels.";
        };
        foreach (var (k, box) in _kindBoxes)
        {
            box.IsCheckedChanged += (_, _) => View.SetShown(k, box.IsChecked == true);
        }
        WaterBox.IsCheckedChanged += (_, _) => View.ShowWater = WaterBox.IsChecked == true;
        UnsavedBox.IsCheckedChanged += (_, _) => View.ShowNewMarkers = UnsavedBox.IsChecked == true;
        LimitBox.IsCheckedChanged += (_, _) => View.LimitMarks = LimitBox.IsChecked == true;
        foreach (var (layer, box) in _overlayBoxes)
        {
            box.IsCheckedChanged += (_, _) => View.SetOverlay(layer, box.IsChecked == true);
        }
        DefaultsButton.Click += (_, _) => ShowPreset(ViewPreset.Defaults);
        AllButton.Click += (_, _) => ShowPreset(ViewPreset.All);
        GroundButton.Click += (_, _) => ShowPreset(ViewPreset.Ground);
        View.OverlaysBuilt += o =>
        {
            void Count(Overlays.Layer l, int n) => _overlayBoxes[l].Content = Ui.Counted((string)_overlayBoxes[l].Tag!, n);
            Count(Overlays.Layer.Markers, o.Locations);
            Count(Overlays.Layer.Wards, o.Wards);
            Count(Overlays.Layer.Stations, o.Stations);
            Count(Overlays.Layer.Flatten, o.Flattened);
        };
        BuilderBox.SelectionChanged += async (_, _) => await BuilderChosen();
        SlopeBox.IsCheckedChanged += (_, _) => View.SlopeColours = SlopeBox.IsChecked == true;
        ContourBox.IsCheckedChanged += (_, _) => ShowContours();
        ContourStepBox.SelectionChanged += (_, _) => ShowContours();
        View.KindCounts += counts =>
        {
            foreach (var (k, box) in _kindBoxes)
            {
                box.Content = Ui.Counted(ObjectKinds.Label(k), counts.GetValueOrDefault(k));
            }
        };
    }

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
        AskPlayerId = () => Dialogs.AskText(this, "Built by", "Player id to write as the builder (the number Valheim keeps for a character):");
        History.Message += t => { MessageText.Text = t; UpdateSaveBar(); };
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
        PlaceInput.Message += t => { MessageText.Text = t; UpdateSaveBar(); };
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
        ConfirmSave = text => Dialogs.Ask(this, "Save into the world", text, "Save");
        Ask = (title, text, yes, no) => Dialogs.Ask(this, title, text, yes, no);
        Tell = text => Dialogs.Tell(this, "Valheim World Editor", text);
        var tools = BuildToolColumn();
        WireShapePanels();
        WireScriptPanel();
        Tools.ToolChanged += OnToolChanged;
        View.SelectTool.Message += (text, _) => MessageText.Text = text;
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
        _info.Text = "Loading the world…";
        Opened += async (_, _) => await OnOpenedAsync(load);
    }

    /// <summary>
    /// Wires the Blueprints panel to the window: its messages, pasting a blueprint, its dialogs and file picker.
    /// </summary>
    private void WireBlueprints()
    {
        Blueprints.Message += t => MessageText.Text = t;
        Blueprints.Pasting += () => { ViewPanelCard.IsVisible = true; StartPaste(); };
        Blueprints.AskDetails = (start, cost) => Dialogs.AskBlueprint(this, start, cost);
        Blueprints.Models = () => _models;
        Blueprints.Confirm = text => Dialogs.Ask(this, "Blueprints", text, "Yes");
        Blueprints.OpenUrl = Launcher.LaunchUriAsync;
        Blueprints.PickFile = () => Pickers.File(StorageProvider, "Import a .blueprint (Homestead, PlanBuild) or .vbuild file",
            new Avalonia.Platform.Storage.FilePickerFileType("Blueprints") { Patterns = BlueprintPatterns });
    }

    /// <summary>
    /// Wires the Inspector to the window: its messages, the object it replaced, its dialog, and the View panel back
    /// when it closes.
    /// </summary>
    private void WireInspector()
    {
        Inspector.Message += t => { MessageText.Text = t; UpdateSaveBar(); };
        Inspector.Replaced += i => View.Select([i]);
        Inspector.Confirm = text => Dialogs.Ask(this, "Contents", text, "Apply anyway");
        Inspector.Closed += () => ViewPanelCard.IsVisible = true;
    }

    /// <summary>
    /// Wires the Select panel to the window and the 3D view: the selection and saved selections, picking a kind,
    /// Inspect (also asked from the view), Claim and Replace.
    /// </summary>
    private void WireSelectPanel()
    {
        SelectPanel.Message += t => MessageText.Text = t;
        SelectPanel.PickKindAsked += PickKind;
        SelectPanel.NameOf = NameOfPrefab;
        SelectPanel.WorldName = () => World?.World.Name is { Length: > 0 } n ? n : View.Scene?.World?.Name is { Length: > 0 } m ? m : null;
        SelectPanel.Things = () => View.Scene?.Things;
        SelectPanel.SelectedIds = () => View.Selected;
        SelectPanel.SelectIds = ids => { Tools.ChooseSelect(); View.Select(ids); };
        SelectPanel.AskName = initial => Dialogs.AskText(this, "Keep the selection", "Name of the selection:", initial);
        SelectPanel.Confirm = text => Dialogs.Ask(this, "Saved selections", text, "Forget");
        SelectPanel.InspectButton.Click += (_, _) => Inspect();
        View.InspectAsked += () => { if (View.Selected.Count == 1) { Inspect(); } };
        SelectPanel.ClaimButton.Click += (_, _) => Claim();
        SelectPanel.ReplaceAsked += ReplaceSelection;
    }

    /// <summary>
    /// Replace (Select panel): the selected objects become the given kind.
    /// </summary>
    /// <param name="prefab">The kind they become.</param>
    private void ReplaceSelection(int prefab)
    {
        View.SelectTool.Commit();
        var sel = View.Selected.ToList();
        if (Session is not { } s || sel.Count == 0)
        {
            MessageText.Text = "Select objects to replace first.";
            return;
        }
        AreaPanel!.Replace(s, sel, prefab);
        View.Select([]);
        UpdateSaveBar();
    }

    /// <summary>
    /// Wires the ground brushes: stamps, the tools' messages, No limit, loading a stamp picture and the brush's
    /// Alt + click.
    /// </summary>
    private void WireGroundTools()
    {
        View.StampClicked += StampAt;
        Tools.Message += t => MessageText.Text = t;
        Tools.NoLimitChanged += OnNoLimitChanged;
        Tools.LoadStampAsked += async () => await LoadStampAsync();
        View.BrushAltClick += OnBrushAltClick;
    }

    /// <summary>
    /// No limit switched (tool options): the session's ground and the Area panel's same switch follow.
    /// </summary>
    /// <param name="on">Whether the ground tools go past the game's ±8 m.</param>
    private void OnNoLimitChanged(bool on)
    {
        Session?.Ground.NoLimit = on;
        // The Area panel shows the same switch under Flatten, Raise and Lower.
        if (AreaPanel != null)
        {
            AreaPanel.NoLimitBox.IsChecked = on;
            AreaPanel.RefreshVolume();
        }
        MessageText.Text = on ? "No limit: the ground tools go past the game's ±8 m. Saving turns that ground into ground discs every player's game counts as generated ground." : "The ground tools keep to the game's ±8 m.";
    }

    /// <summary>
    /// Load stamp…: a grayscale picture chosen from the computer's files becomes a brush shape.
    /// </summary>
    private async Task LoadStampAsync()
    {
        if (await Pickers.File(StorageProvider, "Use a grayscale picture as the brush shape",
            new Avalonia.Platform.Storage.FilePickerFileType("Pictures") { Patterns = PicturePatterns }) is not string path)
        {
            return;
        }
        if (Stamps.FromPicture(File.ReadAllBytes(path)) is { } data)
        {
            Tools.AddStamp(Path.GetFileNameWithoutExtension(path), data);
        }
        else
        {
            MessageText.Text = "That picture could not be read.";
        }
    }

    /// <summary>
    /// Alt + click with a brush: the ground's height there becomes Flatten's target, or with Shift the mask's band.
    /// </summary>
    /// <param name="h">The ground's height clicked.</param>
    /// <param name="shift">Shift held: the mask's band instead.</param>
    private void OnBrushAltClick(float h, bool shift)
    {
        if (shift)
        {
            Mask.PickHeight(h);
            MessageText.Text = $"Mask: ground between {h - 2:0.0} and {h + 2:0.0} m.";
        }
        else
        {
            Tools.FlattenTo(h);
            MessageText.Text = $"Flatten to {h:0.0} m.";
        }
    }

    /// <summary>
    /// Wires the Dungeon panel and its Generate part: building in the dungeon, placing a generated one, going to it
    /// or its entrance, opening it in the Workshop, and the dungeon tool's messages.
    /// </summary>
    private void WireDungeonPanel()
    {
        DungeonPanel.BuildAsked += () => BuildInDungeon(true);
        DungeonPanel.Gen.PlaceAsked += (settings, result) => PlaceGenerated(settings, result);
        DungeonPanel.Gen.DungeonAsked += GoToDungeon;
        DungeonPanel.Gen.EntranceAsked += GoToEntrance;
        DungeonPanel.Gen.WorkshopAsked += async s =>
        {
            try
            {
                await OpenGeneratedInWorkshop(s);
            }
            catch (Exception ex)
            {
                MessageText.Text = $"Could not open the dungeon in the Workshop: {ex.Message}";
            }
        };
        View.Dungeon.Message += t => MessageText.Text = t;
    }

    /// <summary>
    /// Wires the Area panel: its messages, No limit, picking a kind, copy, cut, paste, blueprints and the library,
    /// the clipboard's line, its dialog and its picture and backup pickers.
    /// </summary>
    private void WireAreaPanel()
    {
        AreaPanel.Message += t => { MessageText.Text = t; UpdateSaveBar(); };
        AreaPanel.NoLimitChanged += on => Tools.NoLimitBox.IsChecked = on;
        AreaPanel.PickKindAsked += PickKind;
        AreaPanel.SwitchToSelect += Tools.ChooseSelect;
        AreaPanel.CopyAsked += Copy;
        AreaPanel.CutAsked += Cut;
        AreaPanel.PasteAsked += StartPaste;
        AreaPanel.SaveBlueprintAsked += async () => await Blueprints.Save();
        // The library is a tool of the rail: its panel takes the place of the tool's options.
        AreaPanel.LibraryAsked += () => Tools.ChooseMode(ToolMode.Blueprints);
        Blueprints.Closed += () => Tools.Choose(null);
        View.Paste.Changed += () =>
        {
            var c = View.Paste.Clip;
            AreaPanel.ClipInfo.Text = c == null ? "Copies the shown objects inside the selection, with the ground under their buildings."
                : $"Clipboard{(c.Name != null ? $" ({c.Name})" : "")}: {c.W} × {c.H} m, {c.Objects.Count} object(s).";
        };
        AreaPanel.Confirm = text => Dialogs.Ask(this, "Reset zones", text, "Reset when saving");
        AreaPanel.PickPicture = () => Pickers.File(StorageProvider, "Import a heightmap (grayscale PNG)",
            new Avalonia.Platform.Storage.FilePickerFileType("PNG pictures") { Patterns = PngPatterns });
        AreaPanel.PickFolder = () => Pickers.Folder(StorageProvider, "Choose a backup (or copy) of this world: the folder with _main.<n>.chunks");
    }

    /// <summary>
    /// Gives the Place tool what it reads from the window: the brush, the scene, the mask, names and model boxes.
    /// </summary>
    private void SetUpPlaceTool()
    {
        PlaceTool.Brush = Tools.Brush;
        PlaceTool.Scene = () => View.Scene;
        PlaceTool.Mask = () => Session?.MaskNow();
        PlaceTool.NameOf = NameOfPrefab;
        PlaceTool.ModelBox = ModelBox;
    }

    /// <summary>
    /// Wires the Place panel's messages and its preset dialogs.
    /// </summary>
    private void WirePlacePanel()
    {
        PlacePanel.Message += t => MessageText.Text = t;
        PlacePanel.AskName = () => Dialogs.AskText(this, "Save as preset", "Name of the preset:");
        PlacePanel.Confirm = text => Dialogs.Ask(this, "Delete the preset", text, "Delete");
    }

    /// <summary>
    /// The Help card's Record frame rates switch: frame rates written to the log file while on.
    /// </summary>
    private CheckBox RecordBox()
    {
        var record = new CheckBox { Content = "Record frame rates", FontSize = 12.5 };
        record.IsCheckedChanged += (_, _) => { _perf.On = record.IsChecked == true; _perf.Restart(); if (!_perf.On) { _perf.Flush(); } };
        ToolTip.SetTip(record, Lang.T("view.perfFile", Tips.Of("view.perf"), Ui.Tilde(PerfLog.FilePath)));
        return record;
    }

    /// <summary>
    /// The column of tools on the left: the rail, the tool's options and every tool's panel, each scrolling when the
    /// window is too short; the panels start hidden until their tool is chosen.
    /// </summary>
    private StackPanel BuildToolColumn()
    {
        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Ui.Gap,
            // Under the top bar, above the status bar.
            Margin = new Thickness(10, 70, 10, 58),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { Tools.Rail, Tools.Options, SelectPanel.Card, MeasurePanel.Card, DungeonPanel.Card, ShapePanel.Card, MountainPanel.Card, ScriptPanel.Card, Blueprints.Card, PathPanel.Card, AreaPanel.Card, PastePanel.Card, PlacePanel.Card, PlacePanel.Chooser, BuildPanel.Card, MaskPanel.Card },
        };
        // Every panel of the column scrolls when the window is too short for it (a bar only then).
        foreach (var card in tools.Children.OfType<Border>())
        {
            if (card.Child is Control inner and not ScrollViewer)
            {
                card.Child = null;
                card.Child = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = inner };
            }
        }
        SelectPanel.Card.IsVisible = MeasurePanel.Card.IsVisible = DungeonPanel.Card.IsVisible = ShapePanel.Card.IsVisible = MountainPanel.Card.IsVisible = ScriptPanel.Card.IsVisible = PathPanel.Card.IsVisible = AreaPanel.Card.IsVisible = PastePanel.Card.IsVisible = MaskPanel.Card.IsVisible = PlacePanel.Card.IsVisible = false;
        Tools.Options.VerticalAlignment = VerticalAlignment.Top;
        Tools.Rail.VerticalAlignment = VerticalAlignment.Top;
        return tools;
    }

    /// <summary>
    /// Wires the Shape and Mountain panels to the 3D view's preview and clicks, and the Path panel's cave to the rail.
    /// </summary>
    private void WireShapePanels()
    {
        ShapePanel.Changed += () => View.ShapeRadius = ShapePanel.Radius;
        MountainPanel.Changed += OnMountainChanged;
        View.ShapeClicked += OnShapeClicked;
        Tools.CaveChosen += () =>
        {
            PathPanel.ChooseCave();
            Tools.MarkCave(true);
        };
        PathPanel.ActionChanged += a => Tools.MarkCave(a == PathTool.Action.Cave);
    }

    /// <summary>
    /// The Mountain panel changed: with the Mountain tool on, the preview follows.
    /// </summary>
    private void OnMountainChanged()
    {
        if (Tools.Mode == ToolMode.Mountain)
        {
            View.ShapeRadius = MountainPanel.Spec.Reach;
            View.MountainPreview = Mountain.Shape(MountainPanel.Spec);
        }
    }

    /// <summary>
    /// A click with the Shape or Mountain tool: puts the shape, or the mountain, there.
    /// </summary>
    /// <param name="x">Where, east-west.</param>
    /// <param name="z">Where, north-south.</param>
    private void OnShapeClicked(float x, float z)
    {
        if (Tools.Mode == ToolMode.Mountain)
        {
            _ = PutMountain(x, z);
        }
        else
        {
            PutShape(x, z);
        }
    }

    /// <summary>
    /// Wires the Script panel's Run, Stop, messages and its dialog.
    /// </summary>
    private void WireScriptPanel()
    {
        ScriptPanel.RunAsked += () => _ = RunScript();
        ScriptPanel.StopAsked += () => _stopScript?.Invoke();
        ScriptPanel.Message += t => MessageText.Text = t;
        ScriptPanel.Confirm = text => Dialogs.Ask(this, "Save the script", text, "Replace");
    }

    /// <summary>
    /// Another tool chosen: the 3D view takes it, its panel shows and the others hide, and what the last tool left
    /// (the dungeon's cut, building in the dungeon, the kinds chooser) is put away.
    /// </summary>
    /// <param name="t">The brush chosen, if the tool is a brush.</param>
    private void OnToolChanged(BrushTool? t)
    {
        View.Tool = t;
        View.Mode = Tools.Mode;
        SelectPanel.Card.IsVisible = Tools.SelectMode;
        MeasurePanel.Card.IsVisible = Tools.Mode == ToolMode.Measure;
        if (Tools.Mode == ToolMode.Measure)
        {
            // The ground may have changed since the tape was drawn.
            MeasurePanel.Refresh();
        }
        if (Tools.Mode == ToolMode.Dungeon)
        {
            DungeonPanel.Reload();
        }
        else if (DungeonPanel.Card.IsVisible && !BuildingInDungeon)
        {
            // Not into Build here (it builds under the dungeon's cut): the cut off.
            DungeonPanel.Leave();
        }
        DungeonPanel.Card.IsVisible = Tools.Mode == ToolMode.Dungeon;
        ShapePanel.Card.IsVisible = Tools.Mode == ToolMode.Shape;
        MountainPanel.Card.IsVisible = Tools.Mode == ToolMode.Mountain;
        ScriptPanel.Card.IsVisible = Tools.Mode == ToolMode.Script;
        if (Blueprints.Card.IsVisible != (Tools.Mode == ToolMode.Blueprints))
        {
            Blueprints.Toggle(Tools.Mode == ToolMode.Blueprints);
        }
        View.ShapeRadius = Tools.Mode == ToolMode.Mountain ? MountainPanel.Spec.Reach : ShapePanel.Radius;
        View.MountainPreview = Tools.Mode == ToolMode.Mountain ? Mountain.Shape(MountainPanel.Spec) : null;
        PathPanel.Card.IsVisible = Tools.Mode == ToolMode.Path;
        if (Tools.Mode == ToolMode.Path)
        {
            Tools.MarkCave(View.Path.Act == PathTool.Action.Cave);
        }
        AreaPanel.Card.IsVisible = Tools.Mode == ToolMode.Area;
        PastePanel.Card.IsVisible = Tools.Mode == ToolMode.Paste;
        if (BuildingInDungeon && Tools.Mode != ToolMode.Place)
        {
            if (Tools.Mode != ToolMode.Dungeon)
            {
                DungeonPanel.Leave();
            }
            BuildInDungeon(false);
        }
        PlacePanel.Card.IsVisible = Tools.Mode == ToolMode.Place && !InWorkshop && !BuildingInDungeon;
        BuildPanel.Card.IsVisible = Tools.Mode == ToolMode.Place && (InWorkshop || BuildingInDungeon);
        if (Tools.Mode != ToolMode.Place)
        {
            PlacePanel.Chooser.IsVisible = false;
            PlacePanel.KindsButton.Content = PlacePanel.ChooseKindsText;
        }
        PlaceInput.Refresh();
        MaskPanel.Card.IsVisible = !InWorkshop && Tools.Mode is ToolMode.Brush or ToolMode.Path or ToolMode.Area or ToolMode.Shape or ToolMode.Mountain or ToolMode.Place;
        if (Tools.Mode == ToolMode.Area)
        {
            AreaPanel.Refresh();
        }
    }

    /// <summary>
    /// A stroke ended: its message, Flatten's target when it came from the click, and the save bar.
    /// </summary>
    /// <param name="msg">What the stroke did.</param>
    private void OnStrokeEnded(string msg)
    {
        MessageText.Text = msg;
        if (Tools.Tool == BrushTool.Flatten && Tools.Brush.TargetFromClick)
        {
            Tools.ShowTarget(Tools.Brush.Target);
        }
        UpdateSaveBar();
    }

    /// <summary>
    /// The editor's keys, and Shift held shown by the Place brush (a drag removes).
    /// </summary>
    private void WireKeys()
    {
        KeyDown += OnKey;
        // Shift held: the Place brush shows that a drag removes.
        KeyDown += (_, e) => { if (e.Key is Avalonia.Input.Key.LeftShift or Avalonia.Input.Key.RightShift) { PlaceInput.ShiftHeld(true); } };
        KeyUp += (_, e) => { if (e.Key is Avalonia.Input.Key.LeftShift or Avalonia.Input.Key.RightShift) { PlaceInput.ShiftHeld(false); } };
    }

    /// <summary>
    /// The window is closing: unsaved changes (or an unsaved Workshop building) keep it open and ask first; quitting
    /// anyway closes it again. Otherwise the tunnel to a live game is closed.
    /// </summary>
    /// <param name="e">The closing, cancelled to ask.</param>
    private async Task OnClosingAsync(WindowClosingEventArgs e)
    {
        var pending = World?.Pending ?? Session?.Pending ?? (0, 0, 0, 0);
        if (InWorkshop && !_closeAnyway)
        {
            if (WorkshopDirty)
            {
                e.Cancel = true;
                if (await Ask("Unsaved building", "The building in the Workshop is not saved as a blueprint. Quit without saving it?", "Quit anyway", "Keep building"))
                {
                    _closeAnyway = true;
                    Close();
                }
            }
            return;
        }
        if (_closeAnyway || pending is (0, 0, 0, 0))
        {
            Tunnel.Close();
            return;
        }
        e.Cancel = true;
        if (await Ask("Unsaved changes", $"{(World?.IsLive == true ? "Not applied" : "Unsaved")}: {Describe(pending)}. Quit without {(World?.IsLive == true ? "applying" : "saving")} them?", "Quit anyway", "Keep editing"))
        {
            _closeAnyway = true;
            Close();
        }
    }

    /// <summary>
    /// The panels on the right: under the top bar, one at a time, the View panel shown first.
    /// </summary>
    private void LayOutRightPanels()
    {
        foreach (var right in new[] { ViewPanelCard, History.Card, Inspector.Card, HelpCard })
        {
            right.Margin = new Thickness(10, 70, 10, 58);
            right.HorizontalAlignment = HorizontalAlignment.Right;
            right.VerticalAlignment = VerticalAlignment.Top;
        }
        ViewButton.Classes.Add("on");
    }

    /// <summary>
    /// Ctrl + wheel: pasting, the paste is lowered (into a mountain: Clear the site digs it out) or raised; building
    /// (the Workshop), the piece is lifted; otherwise (false) the wheel is not taken.
    /// </summary>
    /// <param name="dir">The wheel's direction: 1 up, -1 down.</param>
    /// <param name="shift">Shift held: finer steps.</param>
    /// <returns>Whether a tool took it.</returns>
    private bool CtrlWheel(int dir, bool shift)
    {
        // Pasting: the paste lowered (into a mountain: Clear the site digs it out) or raised.
        if (Tools.Mode == ToolMode.Paste)
        {
            PastePanel.OffsetBox.Value = Math.Round((PastePanel.OffsetBox.Value ?? 0) + dir * (shift ? 0.1m : 0.5m), 2);
            MessageText.Text = $"Paste height: {PastePanel.OffsetBox.Value:+0.0#;-0.0#;0} m from the ground clicked (Ctrl + wheel; Shift: 0.1 m).";
            return true;
        }
        if (Tools.Mode != ToolMode.Place || !PlaceTool.Building)
        {
            return false;
        }
        PlaceInput.Lift(dir * (shift ? 0.1f : 0.5f));
        BuildPanel.ShowLift();
        return true;
    }

    /// <summary>
    /// The status bar's cursor readout follows the mouse over the 3D view (and is read again after a stroke).
    /// </summary>
    /// <param name="surface">The control that takes the mouse for the 3D view.</param>
    private void TrackCursor(Control surface)
    {
        surface.PointerMoved += (_, e) => ShowCursor(e.GetPosition(surface));
        surface.PointerExited += (_, _) => ShowCursor(null);
        View.StrokeEnded += _ => ShowCursor(_cursorAt);
    }

    /// <summary>
    /// What the 3D view reports, shown: frame rates, the selection (the Select panel, the Inspector and the status
    /// line follow it), walking or flying, and its status lines.
    /// </summary>
    private void WireViewReports()
    {
        View.StatsChanged += s => _fps.Text = $"{s.Fps} frames/s · {s.WorkMs:0.0} ms of work each · {s.Objects:N0} objects ({s.Instances:N0} model parts in {s.Batches:N0} draws){(s.PendingModels > 0 ? $" · {s.PendingModels} kinds loading" : "")}";
        View.SelectionChanged += _ => FollowSelection();
        View.SelectionChanged += things => _selection.Text = things.Count == 0 ? "" : things.Count == 1
            ? $"Selected: {ThingName(things[0])} at {things[0].Position.X:0.0}, {things[0].Position.Z:0.0} (height {things[0].Position.Y:0.0})"
            : $"Selected: {things.Count} objects ({string.Join(", ", things.GroupBy(ThingName).OrderByDescending(g => g.Count()).Take(4).Select(g => $"{g.Key} ×{g.Count()}"))})";
        View.EyeChanged += e => _eye.Text = e switch
        {
            GlView.EyeMode.Walk => "Walking at eye height: WASD moves (Shift runs), right drag looks around · F flies",
            GlView.EyeMode.Fly => "Flying: WASD moves, Space up, C down (Shift faster) · F back to the usual view",
            _ => "",
        };
        View.Status += t => { Options.Say(t); Dispatcher.UIThread.Post(() => _info.Text = t + "\n" + _info.Text); };
    }

    /// <summary>
    /// The selection changed: the Select panel shows it, and an open Inspector follows a single selected object.
    /// </summary>
    private void FollowSelection()
    {
        SelectPanel.Refresh();
        // Open: it follows the selection.
        var sel = View.Selected;
        if (Inspector.IsOpen && sel.Count == 1 && sel.First() != Inspector.Index)
        {
            Inspect();
        }
    }

    /// <summary>
    /// The window is open: with <paramref name="load"/>, the game's look is checked, Claude's connection applied and
    /// the models read; then the world given on the command line opens (or the start page shows), and the test
    /// driver starts when asked.
    /// </summary>
    /// <param name="load">False (tests): nothing is loaded.</param>
    private async Task OnOpenedAsync(bool load)
    {
        if (!load)
        {
            return;
        }
        Options.Say("window open");
        // The game's look: copied from the player's Valheim when missing or after a game update
        // (when driven by the tests, only whether it is there: they never copy from the game).
        GameLook.Check(Settings);
        // Claude's connection, when chosen in Settings.
        await ClaudeServer.Apply(Settings, this);
        if (!Options.Driver)
        {
            // The copy older editors made of the game's look: nothing reads it any more.
            _ = Task.Run(GameLook.DeleteOldCopy);
            _modelsLoading = Task.Run(ModelStore.Open);
        }
        if (Options.Direct)
        {
            // --world (and --zone): that area in the 3D editor at once.
            try
            {
                var world = await Task.Run(() => WorldSession.Open(WorldScene.FindWorld(Options.World)));
                World = world;
                var scene = await Task.Run(() => WorldScene.Load(world, Options.ZoneX, Options.ZoneZ, Options.Size));
                await ShowEditor(scene);
            }
            catch (Exception ex)
            {
                _info.Text = "Could not open the world: " + ex.Message;
                Options.Say(_info.Text);
            }
        }
        else
        {
            ShowStart();
            // A world folder or a live game given on the command line: as if chosen on the start page.
            if (Options.LiveUrl is string url)
            {
                StartPage!.SetMode("server");
                StartPage.LiveUrl.Text = url;
                StartPage.LiveToken.Text = Options.LiveToken ?? "";
                await StartPage.ConnectUrl();
            }
            else if (Options.Folder is string folder)
            {
                StartPage!.SetMode("offline");
                StartPage.OpenFolder(folder);
            }
        }
        if (Options.Driver)
        {
            Driver.Start(this);
        }
    }
}
