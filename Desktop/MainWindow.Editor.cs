using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The 3D editor's world controls, as the web editor had them: the area moved by one zone (or
// following the view), Discard and (live) Reload and Auto apply in the top bar, the players of a live
// world in the view with their names and Go to, and a note about the locations of the area.
public sealed partial class MainWindow
{
    /// <summary>Moves the area one zone west.</summary>
    internal Button AreaWest { get; } = NavButton("◀", "West");
    /// <summary>Moves the area one zone north.</summary>
    internal Button AreaNorth { get; } = NavButton("▲", "North");
    /// <summary>Moves the area one zone south.</summary>
    internal Button AreaSouth { get; } = NavButton("▼", "South");
    /// <summary>Moves the area one zone east.</summary>
    internal Button AreaEast { get; } = NavButton("▶", "East");
    /// <summary>Turns Follow on or off (the area moves by itself near its edge).</summary>
    internal Button FollowButton { get; } = new Button { Content = "Follow", FontSize = 12.5 }.Classed("ghost");
    /// <summary>Discard: undoes what is not saved or applied yet; enabled while something is.</summary>
    internal Button DiscardButton { get; } = new Button { Content = "Discard", FontSize = 12.5, IsEnabled = false }.Classed("ghost");
    /// <summary>Live: loads the world again from the game; shown for live worlds only.</summary>
    internal Button ReloadButton { get; } = new Button { Content = Icons.With("reload", "Reload"), FontSize = 12.5, IsVisible = false }.Classed("ghost");
    /// <summary>Live: Auto apply, every change sent to the game at once; shown for live worlds only.</summary>
    internal CheckBox AutoApplyBox { get; } = new CheckBox { Content = "Auto", FontSize = 12, IsVisible = false, VerticalAlignment = VerticalAlignment.Center }.Classed("switch");
    /// <summary>
    /// The top bar's area controls: the arrows, the size and Follow; shown when the area belongs to a world.
    /// </summary>
    internal StackPanel AreaNav { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>The area's size, changed on the fly (around the same middle zone).</summary>
    internal ComboBox AreaSizeBox { get; } = new() { ItemsSource = AreaSizes.Select(n => $"{n} × {n}").ToArray(), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0) };
    /// <summary>The sizes the area can have, in zones across.</summary>
    internal static readonly int[] AreaSizes = [3, 5, 7];
    /// <summary>True while <c>ShowAreaSize</c> sets the size box, so its change is not taken for the user's.</summary>
    private bool _fillingSize;
    /// <summary>The note about the area's locations.</summary>
    internal TextBlock LocationText { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#f3d29b")) };
    /// <summary>
    /// The card holding <c>LocationText</c>, over the view's lower left; made in <c>SetUpEditorWorld</c>.
    /// </summary>
    internal Border LocationNote { get; private set; } = null!;
    /// <summary>The live players to go to, by name.</summary>
    internal ComboBox PlayerBox { get; } = new() { FontSize = 12, MinWidth = 130 };
    /// <summary>Goes to the player chosen in <c>PlayerBox</c>.</summary>
    internal Button GoPlayerButton { get; } = new Button { Content = "Go", FontSize = 12 };
    /// <summary>The View panel's players section; shown for live worlds only.</summary>
    internal StackPanel PlayersRow { get; } = new() { Spacing = 2, IsVisible = false };
    /// <summary>The players' names over the view (<c>PlaceLabels</c>).</summary>
    internal Canvas PlayerLabels { get; } = new() { IsHitTestVisible = false };
    /// <summary>The live players: name and world position, as the game last said.</summary>
    internal IReadOnlyList<(string Name, Vector3 World)> LivePlayers { get; private set; } = [];
    /// <summary>Live: asks the game for its players every 2 s.</summary>
    private readonly DispatcherTimer _playersTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    /// <summary>Live: places the players' names over the view ten times a second, following the camera.</summary>
    private readonly DispatcherTimer _labelsTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    /// <summary>Checks Follow (<c>CheckFollow</c>) while the window is open.</summary>
    private readonly DispatcherTimer _followTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    /// <summary>Live: the game asked every 2 s what changed in the zones shown (WorldSession.FollowGame).</summary>
    private readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    /// <summary>Why Follow last waited, so the same reason is not said again at every tick.</summary>
    private string _followSaid = "";
    /// <summary>The editor's settings, loaded when the window is made.</summary>
    internal AppSettings Settings { get; } = AppSettings.Load();
    /// <summary>Writes the settings (Auto, Follow); tests keep them in memory, so no other window reads them.</summary>
    internal Action<AppSettings> SaveSettings { get; set; } = s => s.Save();
    /// <summary>An apply is under way; another was asked for meanwhile (run when this one ends).</summary>
    private bool _applying, _applyAgain;

    /// <summary>An arrow button of the area controls; <paramref name="where"/> names its tip (top.west…).</summary>
    private static Button NavButton(string arrow, string where)
    {
        var b = new Button { Content = arrow, FontSize = 11, Padding = new Thickness(6, 3) }.Classed("ghost");
        b.Tip($"top.{where.ToLowerInvariant()}");
        return b;
    }

    /// <summary>Set up once, from the constructor (after the top bar).</summary>
    private void SetUpEditorWorld()
    {
        AreaNav.Children.Add(new TextBlock { Text = "Area", FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        foreach (var (b, dx, dz) in new[] { (AreaWest, -1, 0), (AreaNorth, 0, 1), (AreaSouth, 0, -1), (AreaEast, 1, 0) })
        {
            b.Click += async (_, _) => await MoveArea(dx, dz);
            AreaNav.Children.Add(b);
        }
        AreaNav.Children.Add(AreaSizeBox);
        AreaSizeBox.Tip("top.areaSize");
        AreaSizeBox.SelectionChanged += async (_, _) =>
        {
            if (!_fillingSize && AreaSizeBox.SelectedIndex >= 0)
            {
                await ResizeArea(AreaSizes[AreaSizeBox.SelectedIndex]);
            }
        };
        AreaNav.Children.Add(FollowButton);
        FollowButton.Tip("top.follow");
        FollowButton.Classes.Set("on", Settings.AreaFollow);
        FollowButton.Click += (_, _) => SetFollow(!Settings.AreaFollow);
        _followTimer.Tick += async (_, _) => await CheckFollow();
        // While the window is open (a running timer keeps the window alive).
        Opened += (_, _) => _followTimer.Start();
        Closed += (_, _) =>
        {
            _followTimer.Stop();
            _gameTimer.Stop();
            _playersTimer.Stop();
            _labelsTimer.Stop();
            MapPage?.Stop();
        };

        DiscardButton.Tip("top.discard");
        DiscardButton.Click += async (_, _) => await DiscardHere();
        ReloadButton.Tip("top.reload");
        ReloadButton.Click += async (_, _) => await ReloadHere();
        AutoApplyBox.Tip("top.auto");
        AutoApplyBox.IsChecked = Settings.AutoApply;
        AutoApplyBox.IsCheckedChanged += async (_, _) =>
        {
            Settings.AutoApply = AutoApplyBox.IsChecked == true;
            SaveSettings(Settings);
            if (Settings.AutoApply && Session is { IsLive: true } s && s.Pending is not (0, 0, 0, 0))
            {
                await ApplyNow();
            }
        };

        var close = new Button { Content = Icons.Make("close", 12), Padding = new Thickness(4) }.Classed("ghost").Tip("note.close");
        close.Click += (_, _) => LocationNote.IsVisible = false;
        Grid.SetColumn(close, 1);
        LocationNote = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2c2416")),
            BorderBrush = new SolidColorBrush(Color.Parse("#7a5a2a")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6),
            Margin = new Thickness(10, 0, 10, 58),
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsVisible = false,
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6, Children = { LocationText, close } },
        };

        GoPlayerButton.Click += async (_, _) => await GoToPlayer(PlayerBox.SelectedItem as string);
        _playersTimer.Tick += async (_, _) => await PollPlayers();
        _gameTimer.Tick += async (_, _) => await FollowGame();
        _labelsTimer.Tick += (_, _) => PlaceLabels();
    }

    /// <summary>The View panel's Go to a player (live worlds).</summary>
    private StackPanel PlayersControl()
    {
        PlayersRow.Children.Add(new TextBlock { Text = "PLAYERS", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Ui.Muted, Margin = new Thickness(0, 8, 0, 2) });
        PlayerBox.Tip("view.player");
        GoPlayerButton.Tip("view.goPlayer");
        PlayersRow.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Go to", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, PlayerBox, GoPlayerButton } });
        return PlayersRow;
    }

    /// <summary>After the top bar's save line is updated: the editor's own buttons.</summary>
    private void UpdateEditorWorld(EditSession s, bool dirty)
    {
        DiscardButton.IsEnabled = dirty;
        ReloadButton.IsVisible = AutoApplyBox.IsVisible = s.IsLive;
    }

    /// <summary>An area was opened: the area buttons, the locations note, the players.</summary>
    private void EditorOpened(WorldScene scene)
    {
        AreaNav.IsVisible = scene.Owner != null;
        ShowAreaSize(scene.Size);
        int n = Overlays.LocationsNear(scene);
        LocationText.Text = n == 0 ? "" : $"{n} location(s) here. The ground already includes the flattening the game does around them (turn on Location markers in View to see where).";
        LocationNote.IsVisible = n > 0;
        _followSaid = "";
        // The history kept from an earlier session, said once when it first shows.
        if (scene.Owner is { Restored: { } kept, RestoredSaid: false } owner && scene.Session?.UndoList.Concat(scene.Session.RedoList).Any(c => c.Earlier) == true)
        {
            owner.RestoredSaid = true;
            int steps = scene.Session.UndoList.Count(c => c.Earlier) + scene.Session.RedoList.Count(c => c.Earlier);
            MessageText.Text = $"History from an earlier session ({kept.SavedAt:g}): {steps} step(s) you can undo, in History.{(kept.Changed ? $" The world {(owner.IsLive ? "may have changed" : "was saved again")} since: you are asked before undoing them." : "")}";
        }
        bool live = scene.Owner?.IsLive == true;
        PlayersRow.IsVisible = live;
        if (live)
        {
            _ = PollPlayers();
            _playersTimer.Start();
            _labelsTimer.Start();
            _gameTimer.Start();
        }
        else
        {
            _gameTimer.Stop();
            _playersTimer.Stop();
            _labelsTimer.Stop();
            SetPlayers([]);
        }
    }

    // ---- Moving the area. The edits and the history stay with the world, the view comes along.

    /// <summary>
    /// Moves the area by zones (+X east, +Z north), keeping the view; the edits and the history stay with the world.
    /// </summary>
    internal async Task MoveArea(int dx, int dz)
    {
        if (View.Scene is not { Owner: not null } s)
        {
            return;
        }
        int mid = s.Size / 2;
        await OpenAreaKeepingView(s.X0 + mid + dx, s.Z0 + mid + dz, s.Size);
    }

    /// <summary>
    /// Opens the area centred on zone (zx, zz); when it did open there, the camera stays where it was in the world.
    /// </summary>
    private async Task OpenAreaKeepingView(int zx, int zz, int size)
    {
        var cam = View.WorldCamera;
        await EditArea(zx, zz, size);
        if (View.Scene is { } ns && ns.X0 + ns.Size / 2 == zx && ns.Z0 + ns.Size / 2 == zz && cam != null)
        {
            View.WorldCamera = cam;
        }
    }

    /// <summary>The area made bigger or smaller around its middle zone: the edits, the history and the view stay.</summary>
    internal async Task ResizeArea(int size)
    {
        if (View.Scene is not { Owner: not null } s || s.Size == size)
        {
            return;
        }
        if (MoveBlocker() is string why)
        {
            MessageText.Text = $"The area cannot change size now: {why}.";
            ShowAreaSize(s.Size);
            return;
        }
        int mid = s.Size / 2;
        MessageText.Text = $"Opening {size} × {size} zones…";
        await OpenAreaKeepingView(s.X0 + mid, s.Z0 + mid, size);
    }

    /// <summary>Shows the area's size in the size box without resizing.</summary>
    private void ShowAreaSize(int size)
    {
        _fillingSize = true;
        AreaSizeBox.SelectedIndex = Array.IndexOf(AreaSizes, size);
        // A size not in the list (1 × 1, from the command line) shows as it is.
        AreaSizeBox.PlaceholderText = $"{size} × {size}";
        _fillingSize = false;
    }

    /// <summary>Turns Follow on or off and keeps the choice in the settings.</summary>
    internal void SetFollow(bool on)
    {
        Settings.AreaFollow = on;
        SaveSettings(Settings);
        FollowButton.Classes.Set("on", on);
        MessageText.Text = on ? "Follow: the area moves by itself when you look near its edge." : "Follow is off: move the area with the arrows.";
        _followSaid = "";
    }

    /// <summary>What stops the area from moving now (Follow waits): something unfinished would be lost.</summary>
    internal string? MoveBlocker()
    {
        if (View.Selected.Count > 0)
        {
            return "objects are selected (Esc clears)";
        }
        if (Session?.Stroking == true)
        {
            return "a stroke is on";
        }
        if (View.Path.Points.Count > 0)
        {
            return "a path is drawn (apply or clear it)";
        }
        if (_applying)
        {
            return "changes are still being sent";
        }
        return null;
    }

    /// <summary>
    /// Follow: when the view's centre comes within 12 m of the area's edge, the area moves to the zone
    /// under it (one zone on when that is the middle one already). The zone it moves to, or null.
    /// </summary>
    internal async Task<(int X, int Z)?> CheckFollow()
    {
        if (!Settings.AreaFollow || _pages.Content != _editorPage || View.Scene is not { Owner: not null } s)
        {
            return null;
        }
        var t = View.Camera.Target;
        bool nearX = MathF.Abs(t.X) > (s.W - 1) / 2f - 12, nearZ = MathF.Abs(t.Z) > (s.H - 1) / 2f - 12;
        if (!nearX && !nearZ)
        {
            _followSaid = "";
            return null;
        }
        if (MoveBlocker() is string why)
        {
            if (_followSaid != why)
            {
                MessageText.Text = $"Follow waits: {why}.";
            }
            _followSaid = why;
            return null;
        }
        float wx = t.X + s.Cx, wz = -t.Z + s.Cz;
        int czx = s.X0 + s.Size / 2, czz = s.Z0 + s.Size / 2;
        int zx = (int)MathF.Floor((wx + 32) / 64), zz = (int)MathF.Floor((wz + 32) / 64);
        if (nearX && zx == czx)
        {
            zx += Math.Sign(wx - czx * 64);
        }

        if (nearZ && zz == czz)
        {
            zz += Math.Sign(wz - czz * 64);
        }

        if (!nearX)
        {
            zx = czx;
        }

        if (!nearZ)
        {
            zz = czz;
        }

        if (zx == czx && zz == czz)
        {
            return null;
        }
        MessageText.Text = "Moving the area…";
        await OpenAreaKeepingView(zx, zz, s.Size);
        return (zx, zz);
    }

    // ---- Discard and Reload, here in the editor.

    /// <summary>
    /// Undoes the steps not saved or applied yet, keeping the view, the tool and the rest of the history.
    /// Pending changes with no step here (made in another area, or before) need the world read again.
    /// </summary>
    internal async Task DiscardHere()
    {
        if (Session is not { } s)
        {
            return;
        }
        View.SelectTool.Commit();
        if (s.Pending is (0, 0, 0, 0))
        {
            MessageText.Text = "There is nothing to discard.";
            return;
        }
        int steps = s.UnappliedSteps;
        if (!await Ask("Discard", $"Discard the {(s.IsLive ? "changes not applied yet" : "unsaved changes")} ({Describe(s.Pending)})?{(steps > 0 ? $" The last {steps} step(s) in History are undone." : "")}", "Discard", "Keep them"))
        {
            return;
        }
        s.UndoUnapplied();
        UpdateSaveBar();
        History.Refresh();
        if (s.Pending is (0, 0, 0, 0))
        {
            MessageText.Text = "Discarded. Everything else is kept.";
            return;
        }
        if (s.Scene.Owner is not { } w || !await Ask("Discard", "Some of the changes waiting have no step in this area's history (made in another area, or earlier), so they can only be discarded by reading the world again. Read it again now?", "Read it again", "Keep them"))
        {
            return;
        }
        Busy("Reading the world again…");
        try
        {
            await Task.Run(w.Discard);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
        {
            MessageText.Text = "Could not read the world again: " + ex.Message;
            Busy(null);
            return;
        }
        Busy(null);
        await ReopenArea(s.Scene);
        MessageText.Text = "Discarded: the world was read again.";
    }

    /// <summary>Live: the world loaded again from the game (asks first when something waits).</summary>
    internal async Task ReloadHere()
    {
        if (Session is not { IsLive: true, Scene.Owner: { } w } s)
        {
            return;
        }
        if (s.Pending is not (0, 0, 0, 0) && !await Ask("Reload", "Reload the world from the game? Your changes not applied yet are dropped.", "Reload", "Keep them"))
        {
            return;
        }
        Busy("Loading the world from the game…");
        try
        {
            await w.Reload();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException or System.Text.Json.JsonException)
        {
            Busy(null);
            MessageText.Text = "Could not reach the game: " + ex.Message;
            return;
        }
        Busy(null);
        await ReopenArea(s.Scene);
        MessageText.Text = "The world was loaded again from the game.";
    }

    /// <summary>The same area again (the world was read again), the view where it was.</summary>
    private async Task ReopenArea(WorldScene old)
    {
        Session = null;
        await OpenAreaKeepingView(old.X0 + old.Size / 2, old.Z0 + old.Size / 2, old.Size);
    }

    // ---- Live: Apply (the button, and every change when Auto is on). Calls while one runs are merged.

    /// <summary>
    /// Live: Apply (the button, and every change when Auto is on). Calls while one runs are merged into one more run
    /// after it.
    /// </summary>
    internal async Task ApplyNow()
    {
        if (_applying)
        {
            _applyAgain = true;
            return;
        }
        _applying = true;
        try
        {
            do
            {
                _applyAgain = false;
                if (Session is not { IsLive: true } s)
                {
                    break;
                }
                SaveButton.IsEnabled = false;
                MessageText.Text = "Applying to the running game…";
                var o = await s.ApplyLive(ConfirmOverGame);
                MessageText.Text = o.Message;
                if (o.Declined)
                {
                    UpdateSaveBar();
                    break;
                }
                UpdateSaveBar();
                History.Refresh();
            }
            while (_applyAgain);
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>A change made with Auto on: to the game at once.</summary>
    private void AutoApply(EditSession s)
    {
        if (Settings.AutoApply && s == Session && s.IsLive && !s.Stroking)
        {
            _ = ApplyNow();
        }
    }

    // ---- Live: following the game in the zones shown.
    /// <summary>True while <c>FollowGame</c> runs, so the timer does not start another.</summary>
    private bool _followingGame;
    /// <summary>The world whose plugin is too old to follow (asked again for another world).</summary>
    private WorldSession? _gameTooOld;
    /// <summary>
    /// Read from the game, not merged yet (a stroke was being drawn, an apply was under way): merged at
    /// the next tick, before anything new is read.
    /// </summary>
    private WorldSession.Fetched? _gameFetched;

    /// <summary>
    /// Live: reads what changed in the game in the zones shown and merges it into the open area (the timer's tick).
    /// A game that does not answer is asked again at the next tick.
    /// </summary>
    internal async Task FollowGame()
    {
        if (_followingGame || Session is not { IsLive: true, Scene.Owner: { } owner } s || _gameTooOld == owner)
        {
            return;
        }
        _followingGame = true;
        try
        {
            if (_gameFetched == null && !s.Stroking)
            {
                var zones = new List<(int, int)>();
                for (int z = 0; z < s.Scene.Size; z++)
                {
                    for (int x = 0; x < s.Scene.Size; x++)
                    {
                        zones.Add((s.Scene.X0 + x, s.Scene.Z0 + z));
                    }
                }
                _gameFetched = await owner.FetchGame(zones);
            }
            // Merged and shown at once, on this thread: nothing changes the world or the area in between.
            // The area open now gets it, whichever it is (taking it twice changes nothing).
            if (Session is not { Scene.Owner: { } now } open || now != owner)
            {
                _gameFetched = null;
                return;
            }
            if (_gameFetched is { } fetched && !open.Stroking && owner.MergeGame(fetched) is { } followed)
            {
                _gameFetched = null;
                if (followed.Any && open.TakeGameChanges(followed))
                {
                    SayFromGame(followed);
                }
            }
            // Changes of the editor's undone where the game changed the ground meanwhile: the game's now.
            else if (_gameFetched == null && !open.Stroking && owner.TakeWaitingGround() is { Count: > 0 } waiting)
            {
                open.TakeGameChanges(new WorldSession.Followed(new Save.WorldSave.Merged([], [], [], []), [], waiting, []));
                MessageText.Text = $"From the game: the ground of {waiting.Count} zone(s), where your changes were undone.";
            }
            UpdateSaveBar();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("too old", StringComparison.Ordinal))
        {
            // An older plugin on the server: no following (Reload still reads the whole world again).
            _gameTooOld = owner;
            MessageText.Text = "The WorldEditorBridge plugin on the server is older than this editor: update it to see what players change while you edit (Reload reads the world again meanwhile).";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException or FormatException)
        {
            // The game does not answer right now: asked again at the next tick.
        }
        finally
        {
            _followingGame = false;
        }
    }

    /// <summary>
    /// Said in the status bar: what came from the game that shows (objects come, go or move; ground). A
    /// change of data alone (a fire burning down, a chest used) is taken in without a word.
    /// </summary>
    private void SayFromGame(WorldSession.Followed f)
    {
        var parts = new List<string>();
        int added = f.Merged.Added.Count, gone = f.Merged.Vanished.Count + f.Ours.Count;
        if (added > 0)
        {
            parts.Add($"{added} new object(s)");
        }

        if (gone > 0)
        {
            parts.Add($"{gone} removed");
        }

        if (f.Ground.Count > 0)
        {
            parts.Add($"the ground of {f.Ground.Count} zone(s)");
        }

        string text = parts.Count > 0 ? $"From the game: {string.Join(", ", parts)}." : "";
        if (f.Kept.Count > 0)
        {
            text += $" The game changed the ground of zone(s) {string.Join("; ", f.Kept.Select(z => $"{z.X}, {z.Z}"))}, where you have changes not applied: yours are kept, and applying asks first.";
        }
        if (text != "")
        {
            MessageText.Text = text.Trim();
        }
    }

    /// <summary>
    /// Asked by Apply live before writing over ground the game changed where the editor has changes not
    /// applied yet (whatever was dug or built there since). True to go on.
    /// </summary>
    internal async Task<bool> ConfirmOverGame(IReadOnlyList<(int X, int Z)> over) =>
        await Ask("Apply over the game's changes",
            $"The ground of {over.Count} zone(s) ({string.Join("; ", over.Take(6).Select(z => $"{z.X}, {z.Z}"))}{(over.Count > 6 ? "…" : "")}) was changed in the game since you started editing it. "
            + "Applying writes your ground there: what players dug, flattened or paved in those zones since may be erased.\n\nApply anyway?", "Apply", "Not now");

    // ---- Live players.

    /// <summary>Live: the players' names and positions from the game; the last ones stay when it does not answer.</summary>
    internal async Task PollPlayers()
    {
        if (View.Scene?.Owner?.Live is not { } live)
        {
            return;
        }
        try
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(await live.Players())?.AsArray();
            var players = json?.Where(p => p?["name"] != null).Select(p => ((string)p!["name"]!, new Vector3((float)(p["x"] ?? 0), (float)(p["y"] ?? 0), (float)(p["z"] ?? 0)))).ToList() ?? [];
            SetPlayers(players);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            // The game does not answer right now: the last positions stay.
        }
    }

    /// <summary>Shows these players: in the view, the Go to list and the names over the view.</summary>
    internal void SetPlayers(IReadOnlyList<(string Name, Vector3 World)> players)
    {
        LivePlayers = players;
        View.Players = players;
        var names = players.Select(p => p.Name).ToList();
        string? chosen = PlayerBox.SelectedItem as string;
        if (!(PlayerBox.ItemsSource is List<string> old && old.SequenceEqual(names)))
        {
            PlayerBox.ItemsSource = names;
            PlayerBox.SelectedItem = chosen != null && names.Contains(chosen) ? chosen : names.FirstOrDefault();
        }
        GoPlayerButton.IsEnabled = names.Count > 0;
        ToolTip.SetTip(PlayerBox, Tips.Of("view.player") + "\n" + string.Join("\n", players.Select(p => $"{p.Name}: {p.World.X:0}, {p.World.Z:0}")));
        PlaceLabels();
    }

    /// <summary>The players' names over their posts (4 m above their feet), where the camera sees them.</summary>
    internal void PlaceLabels()
    {
        PlayerLabels.Children.Clear();
        if (View.Scene is not { } s || LivePlayers.Count == 0 || _pages.Content != _editorPage)
        {
            return;
        }
        var size = View.Bounds.Size;
        foreach (var (name, w) in LivePlayers)
        {
            var q = Vector4.Transform(new Vector4(w.X - s.Cx, w.Y + 4, -(w.Z - s.Cz), 1), View.ViewProj);
            if (q.W <= 0.01f)
            {
                continue;
            }
            double x = (q.X / q.W + 1) / 2 * size.Width, y = (1 - q.Y / q.W) / 2 * size.Height;
            if (x < 0 || y < 0 || x > size.Width || y > size.Height)
            {
                continue;
            }
            var label = new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#bfeaff")), Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), Padding = new Thickness(4, 1) };
            Canvas.SetLeft(label, x - 4 * name.Length - 4);
            Canvas.SetTop(label, y - 10);
            PlayerLabels.Children.Add(label);
        }
    }

    /// <summary>Go to a player: right here when they are in the area, else the area around them.</summary>
    internal async Task GoToPlayer(string? name)
    {
        if (name == null || View.Scene is not { } s || LivePlayers.FirstOrDefault(p => p.Name == name) is not { Name: not null } p)
        {
            return;
        }
        float gx = p.World.X - (s.X0 * 64 - 32), gz = p.World.Z - (s.Z0 * 64 - 32);
        if (!(gx > 2 && gz > 2 && gx < s.W - 3 && gz < s.H - 3) && s.Owner != null)
        {
            int zx = (int)MathF.Floor((p.World.X + 32) / 64), zz = (int)MathF.Floor((p.World.Z + 32) / 64);
            await EditArea(zx, zz, s.Size);
        }
        View.Focus(p.World);
        MessageText.Text = $"Looking at {name}.";
    }
}
