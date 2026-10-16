using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// Blueprints (WorldEdit's schematics): copies kept under a name, pasted again in any area or world. They
/// are Homestead's blueprints (Homestead.cs): .blueprint files with a picture in Homestead's folder of
/// Valheim's save folder, the same files Homestead lists in game, so a building saved here is built in
/// game with Homestead (and one saved in game is pasted here). The panel says whether Homestead is
/// installed in the game's BepInEx (or a mod manager profile), and warns when it is not. Blueprints
/// kept in the editor's own format before (the data folder's blueprints/) are still listed, and can be
/// moved to Homestead. Other mods' files (PlanBuild .blueprint, .vbuild) are imported, and written.
/// Each blueprint shows its picture (BuildingPicture), what it costs to build in game (PieceCost), its
/// description and tags; the search looks through all of them.
/// </summary>
public sealed class BlueprintsPanel
{
    /// <summary>The panel itself, hidden until opened.</summary>
    public Control Card { get; }
    /// <summary>The editor's own blueprints from before (blueprints/ of the data folder).</summary>
    public BlueprintStore Store { get; set; } = BlueprintStore.Default;
    /// <summary>Whether Homestead is installed, and its blueprint folder (tests give their own).</summary>
    internal Func<Homestead.Status> FindHomestead { get; set; } = () => Homestead.Find(AppSettings.Load());
    /// <summary>Filters the list: every word somewhere in a blueprint's name, creator, description or tags.</summary>
    internal TextBox Search { get; } = new() { PlaceholderText = "Search blueprints", FontSize = 12 };
    /// <summary>Imports another mod's blueprint file (see <see cref="Import"/>).</summary>
    internal Button ImportButton { get; } = new() { Content = "Import file…", FontSize = 12 };
    /// <summary>Opens an empty Workshop to build a new blueprint in.</summary>
    internal Button NewButton { get; } = new() { Content = "New in Workshop", FontSize = 12 };
    /// <summary>Edit (a blueprint file) or New in Workshop (null): the window opens the Workshop.</summary>
    public event Action<string?>? EditAsked;
    /// <summary>Opens Homestead's download page; shown only while it is not installed.</summary>
    internal Button GetHomesteadButton { get; } = new() { Content = "Get Homestead", FontSize = 11, Padding = new Thickness(6, 1) };
    /// <summary>The blueprint rows.</summary>
    internal StackPanel List { get; } = new() { Spacing = 4 };
    /// <summary>Whether Homestead is installed, and what that means for the blueprints.</summary>
    internal TextBlock Banner { get; } = new() { FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
    /// <summary>The banner's box, coloured as a warning while Homestead is not installed.</summary>
    private readonly Border _bannerBox;
    /// <summary>Where the blueprints are kept.</summary>
    private readonly TextBlock _folder = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>The open world's scene, null when none is open.</summary>
    private readonly Func<WorldScene?> _scene;
    /// <summary>The paste tool whose clipboard blueprints are saved from and loaded onto.</summary>
    private readonly PasteTool _paste;
    /// <summary>A line for the status bar.</summary>
    public event Action<string>? Message;
    /// <summary>✕: the window goes back to View.</summary>
    public event Action? Closed;
    /// <summary>A blueprint went onto the clipboard: the window starts pasting.</summary>
    public event Action? Pasting;
    /// <summary>Asks for a blueprint's name, description and tags (given the ones to start from, and what it costs).</summary>
    /// <remarks>Null: cancelled.</remarks>
    internal Func<Homestead.Details, string, Task<Homestead.Details?>> AskDetails { get; set; } = (_, _) => Task.FromResult<Homestead.Details?>(null);
    /// <summary>The game's models, for the blueprints' pictures (null: their boxes are drawn).</summary>
    internal Func<ModelStore?> Models { get; set; } = () => null;
    /// <summary>Asks a yes/no question (the window's dialog): before deleting or replacing a blueprint.</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
    /// <summary>Asks for a blueprint file to import (the window's file picker).</summary>
    internal Func<Task<string?>> PickFile { get; set; } = () => Task.FromResult<string?>(null);
    /// <summary>Opens a web page in the browser.</summary>
    internal Func<Uri, Task<bool>> OpenUrl { get; set; } = _ => Task.FromResult(false);

    /// <summary>Homestead's blueprints have ids "hs:&lt;file name&gt;"; the editor's older ones their store id.</summary>
    private const string HomesteadId = "hs:";

    /// <summary>Builds the panel (hidden).</summary>
    /// <param name="paste">The paste tool whose clipboard is saved and filled.</param>
    /// <param name="scene">The open world's scene, null when none is open.</param>
    public BlueprintsPanel(PasteTool paste, Func<WorldScene?> scene)
    {
        _paste = paste;
        _scene = scene;
        var close = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(6, 0) }.Tip("card.close");
        Search.Tip("blueprints.search");
        close.Click += (_, _) => { Card!.IsVisible = false; Closed?.Invoke(); };
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { Render(); } };
        ImportButton.Tip("blueprints.import");
        ImportButton.Click += async (_, _) => { if (await PickFile() is string path) { Import(path); } };
        NewButton.Tip("blueprints.new");
        NewButton.Click += (_, _) => EditAsked?.Invoke(null);
        GetHomesteadButton.Tip("blueprints.getHomestead");
        GetHomesteadButton.Click += async (_, _) => await OpenUrl(new Uri(Homestead.PageUrl));
        _bannerBox = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6),
            Child = new StackPanel { Spacing = 4, Children = { Banner, GetHomesteadButton } },
        };
        Card = new Border
        {
            Background = Ui.Panel,
            BorderBrush = Ui.Line,
            BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = Ui.Pad,
            VerticalAlignment = VerticalAlignment.Top,
            IsVisible = false,
            Child = new StackPanel
            {
                Width = 340,
                Spacing = 6,
                Children =
                {
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "Blueprints", FontSize = 14, FontWeight = FontWeight.SemiBold }, Col(close, 1) } },
                    _bannerBox,
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { Search, Col(ImportButton, 1) } },
                    NewButton,
                    new ScrollViewer { MaxHeight = 640, Content = List },
                    _folder,
                },
            },
        };
    }

    /// <summary>Puts a control in a grid column.</summary>
    private static Control Col(Control c, int col)
    {
        Grid.SetColumn(c, col);
        return c;
    }

    /// <summary>Shows or hides the panel, and refreshes it.</summary>
    /// <param name="open">True shows, false hides, null switches.</param>
    public void Toggle(bool? open = null)
    {
        Card.IsVisible = open ?? !Card.IsVisible;
        Refresh();
    }

    /// <summary>The editor's older blueprints, as last listed.</summary>
    private List<BlueprintStore.Summary> _older = [];
    /// <summary>Homestead's blueprints, as last listed.</summary>
    private List<Homestead.Entry> _homestead = [];

    /// <summary>Homestead as last looked for (on refresh and before saving or importing; first when needed).</summary>
    internal Homestead.Status Status { get => field ??= FindHomestead(); private set; }

    /// <summary>Looks for Homestead again (installed or moved since), without listing the blueprints.</summary>
    /// <returns>What was found, now <see cref="Status"/>.</returns>
    internal Homestead.Status LookAgain() => Status = FindHomestead();

    /// <summary>Looks for Homestead again and, while the panel is shown, lists the blueprints again.</summary>
    public void Refresh()
    {
        LookAgain();
        if (!Card.IsVisible)
        {
            return;
        }
        _homestead = Homestead.List(Status.Folder);
        _older = Store.List();
        if (Status.Installed)
        {
            Banner.Text = $"Homestead {Status.Version} is installed ({string.Join(", ", Status.Where.Distinct())}): these are its blueprints, shared with its hammer tab in game.";
            Banner.Foreground = Ui.Muted;
            _bannerBox.Background = Brushes.Transparent;
            GetHomesteadButton.IsVisible = false;
        }
        else
        {
            Banner.Text = "Homestead is not installed in your Valheim's BepInEx (nor in a mod manager profile). Blueprints are still saved where it looks for them, "
                + "so they show in its hammer tab, ready to build in game, once you install it.";
            Banner.Foreground = new SolidColorBrush(Color.FromRgb(240, 180, 90));
            _bannerBox.Background = new SolidColorBrush(Color.FromArgb(40, 240, 160, 60));
            GetHomesteadButton.IsVisible = true;
        }
        _folder.Text = $"Homestead's blueprints: {Ui.Tilde(Status.Folder)}." + (_older.Count > 0 ? $" Older editor blueprints: {Ui.Tilde(Store.Directory)}." : "");
        Render();
    }

    /// <summary>One row: picture, name, what it holds, its buttons.</summary>
    /// <param name="picture">The picture, null: none.</param>
    /// <param name="name">The blueprint's name.</param>
    /// <param name="meta">What it holds, where from.</param>
    /// <param name="buttons">Adds the row's buttons through the function it is given (text, tip key, click).</param>
    /// <param name="cost">What it costs to build, shown under the meta line (null: not shown).</param>
    /// <param name="description">Shown in up to three lines (null or blank: not shown).</param>
    /// <param name="tags">Shown as chips (null or none: not shown).</param>
    private Border Row(Bitmap? picture, string name, string meta, Action<Func<string, string, Action, Button>> buttons, string? cost = null, string? description = null, IReadOnlyList<string>? tags = null)
    {
        var acts = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
        Button Act(string text, string tip, Action a)
        {
            // By its tip key, which Claude knows it by (and is kept from the ones that write or delete files).
            var btn = new Button { Content = text, FontSize = 11, Padding = new Thickness(6, 1) }.Tip(tip);
            btn.Click += (_, _) => a();
            acts.Children.Add(btn);
            return btn;
        }
        buttons(Act);
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = name, FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = meta, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap });
        if (cost != null)
        {
            text.Children.Add(new TextBlock { Text = cost, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(214, 190, 140)), TextWrapping = TextWrapping.Wrap });
        }
        if (!string.IsNullOrWhiteSpace(description))
        {
            text.Children.Add(new TextBlock { Text = description, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis });
        }
        if (tags is { Count: > 0 })
        {
            var chips = new WrapPanel { ItemSpacing = 3, LineSpacing = 3 };
            foreach (string tag in tags)
            {
                chips.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(50, 120, 160, 220)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 0),
                    Child = new TextBlock { Text = tag, FontSize = 10.5 },
                });
            }
            text.Children.Add(chips);
        }
        text.Children.Add(acts);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("80,*"),
            ColumnSpacing = 8,
            Children =
            {
                new Image { Source = picture, Width = 80, Height = 80, VerticalAlignment = VerticalAlignment.Top },
                Col(text, 1),
            },
        };
        return new Border { Padding = new Thickness(4), CornerRadius = new CornerRadius(6), Child = grid };
    }

    /// <summary>Fills the list with the blueprints that match the search: Homestead's first, then the older ones.</summary>
    private void Render()
    {
        List.Children.Clear();
        string q = Search.Text?.Trim() ?? "";
        bool Match(string name) => q == "" || name.Contains(q, StringComparison.OrdinalIgnoreCase);
        var homestead = _homestead.Where(e => e.Matches(q)).ToList();
        var older = _older.Where(b => Match(b.Name)).ToList();
        if (homestead.Count + older.Count == 0)
        {
            List.Children.Add(new TextBlock
            {
                Text = _homestead.Count + _older.Count > 0 ? "Nothing matches." : "No blueprints yet. Copy something (Area or Select tool, Ctrl+C), then “Save blueprint…”.",
                FontSize = 11,
                Foreground = Ui.Muted,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }
        foreach (var e in homestead)
        {
            string id = HomesteadId + Path.GetFileName(e.Path);
            string meta = $"{e.Pieces} piece(s){(e.Creator is { Length: > 0 } c ? $" · by {c}" : "")}{(e.World != null ? $" · from {e.World}" : "")}";
            var cost = Terrain.PieceCost.Of(e.Kinds);
            List.Children.Add(Row(BlueprintPictures.File(e.Picture), e.Name, meta, act =>
            {
                act("Paste", "blueprints.paste", () => Paste(id));
                act("Edit", "blueprints.edit", () => EditAsked?.Invoke(e.Path));
                act("Details", "blueprints.details", async () => await EditDetails(e));
                act(".vbuild", "blueprints.vbuild", () => Export(id, "vbuild"));
                act("Delete", "blueprints.delete", async () => await Delete(id, e.Name));
            }, "Cost: " + cost.Describe(), e.Description, e.Tags));
        }
        if (older.Count > 0)
        {
            List.Children.Add(new TextBlock { Text = "Older editor blueprints", FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        }
        foreach (var b in older)
        {
            string meta = $"{b.W} × {b.H} m · {b.Objects} object(s){(b.Ground ? " · ground" : "")}{(b.World != null ? $" · from {b.World}" : "")}{(b.Source != null ? $" · {b.Source}" : "")}";
            List.Children.Add(Row(BlueprintPictures.Url(b.Thumb), b.Name, meta, act =>
            {
                act("Paste", "blueprints.paste", () => Paste(b.Id));
                act("To Homestead", "blueprints.toHomestead", () => ToHomestead(b.Id));
                act(".vbuild", "blueprints.vbuildOld", () => Export(b.Id, "vbuild"));
                act("Delete", "blueprints.deleteOld", async () => await Delete(b.Id, b.Name));
            }));
        }
    }

    /// <summary>A blueprint as a copy (the clipboard format), its name and the world it came from, or null.</summary>
    /// <param name="id">The blueprint's id ("hs:" and its file name, or its store id).</param>
    /// <param name="problem">Why a blueprint file could not be made a copy, when that is why (null otherwise).</param>
    private (JsonObject Clip, string Name, string? World, bool Dungeon)? Load(string id, out string? problem)
    {
        problem = null;
        if (id.StartsWith(HomesteadId, StringComparison.Ordinal))
        {
            string path = Path.Combine(Status.Folder, id[HomesteadId.Length..]);
            if (Path.GetFileName(path) != id[HomesteadId.Length..] || !File.Exists(path))
            {
                return null;
            }
            string text = File.ReadAllText(path);
            var parsed = BlueprintFormats.Parse(path, text);
            var world = _scene()?.World;
            JsonObject clip;
            try
            {
                clip = BlueprintFormats.ToClip(parsed, n => world?.CanCreate(StableHash.OfName(n)) ?? Terrain.PrefabCatalog.Get(StableHash.OfName(n)) != null, out _);
            }
            catch (InvalidDataException ex)
            {
                problem = $"That blueprint could not be read: {ex.Message}.";
                return null;
            }
            // A dungeon by its tags: pasted, it goes up behind an entrance.
            return (clip, parsed.Name, null, new Homestead.Details(parsed.Name, "", Homestead.Details.ParseTags(TagsOf(text))).Dungeon);
        }
        if (Store.Read(id) is not string json || JsonNode.Parse(json) is not JsonObject doc || doc["clip"] is not JsonObject c)
        {
            return null;
        }
        return (c, (string?)doc["name"] ?? id, (string?)doc["world"], false);
    }

    /// <summary>Writes a Homestead blueprint and its picture (named after the blueprint, replacing one of that name).</summary>
    /// <returns>The path of the file written.</returns>
    private string WriteHomestead(JsonObject clip, Homestead.Details details, string? world)
    {
        Directory.CreateDirectory(Status.Folder);
        string path = Path.Combine(Status.Folder, Homestead.FileName(details.Name));
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, Homestead.Write(clip, details.Name, "Valheim World Editor", world, DateTime.Now, details.Description, details.Tags));
        File.Move(tmp, path, overwrite: true);
        File.WriteAllBytes(Path.ChangeExtension(path, ".png"), BuildingPicture.Draw(BuildingPicture.FromClip(clip), Models()));
        return path;
    }

    /// <summary>Details with only a name.</summary>
    private static Homestead.Details Named(string name) => new(name, "", []);

    /// <summary>Asks for the details (none: null), and before replacing a blueprint of that name.</summary>
    /// <param name="start">The details to start from.</param>
    /// <param name="clip">The copy, for what it costs.</param>
    /// <param name="keep">The file name being saved over on purpose (the one the building was opened from): no question for it.</param>
    private async Task<Homestead.Details?> Ask(Homestead.Details start, JsonObject clip, string? keep = null)
    {
        var cost = Terrain.PieceCost.Of(BuildingPicture.FromClip(clip).Select(p => p.Name));
        var d = await AskDetails(start, cost.Describe(8));
        if (d == null || string.IsNullOrWhiteSpace(d.Name))
        {
            return null;
        }
        d = d with { Name = d.Name.Trim(), Description = d.Description.Trim() };
        if (!string.Equals(Homestead.FileName(d.Name), keep, StringComparison.OrdinalIgnoreCase) && HomesteadExists(d.Name)
            && !await Confirm($"A blueprint called “{d.Name}” already exists. Replace it?"))
        {
            return null;
        }
        return d;
    }

    /// <summary>Whether a Homestead blueprint of that name is already there.</summary>
    private bool HomesteadExists(string name) => File.Exists(Path.Combine(Status.Folder, Homestead.FileName(name)));

    /// <summary>Whether a copy holds ground (heights other than the "none" marker, -32768).</summary>
    private static bool HasGround(JsonObject clip) => clip["rel"] is JsonArray rel && rel.Any(v => v != null && (int)v != -32768);

    /// <summary>The end of a message while Homestead is not installed, empty when it is.</summary>
    private string Reminder => Status.Installed ? "" : " Homestead is not installed in your game yet: it shows there once you install it.";

    /// <summary>
    /// Saves the clipboard as a Homestead blueprint (asks for the name; one with that name is replaced
    /// after asking). Homestead blueprints hold pieces only: copied ground is not kept.
    /// </summary>
    /// <returns>The name it was saved under, null when not saved.</returns>
    public async Task<string?> Save()
    {
        var clip = _paste.Clip;
        if (clip == null)
        {
            Message?.Invoke("Copy something first (Area or Select tool, Ctrl+C), then save it as a blueprint.");
            return null;
        }
        Status = FindHomestead();
        var json = CopyFormat.ToJson(clip.WithoutBuilders());
        if (await Ask(Named(clip.Name ?? $"Blueprint {DateTime.Now:yyyy-MM-dd HH.mm}"), json) is not { } details)
        {
            return null;
        }
        string name = details.Name;
        clip.Name = name;
        json = CopyFormat.ToJson(clip.WithoutBuilders());
        try
        {
            WriteHomestead(json, details, _scene()?.World?.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message?.Invoke($"Could not save the blueprint: {ex.Message}");
            return null;
        }
        Message?.Invoke($"Saved the blueprint “{name}” ({clip.Objects.Count} object(s)) for Homestead."
            + (HasGround(json) ? " Its ground shape is not kept: Homestead blueprints hold pieces only." : "") + Reminder);
        Refresh();
        return name;
    }

    /// <summary>
    /// The Workshop's building as a Homestead blueprint (asks for the details, suggested; one with that
    /// name is replaced after asking, except the file it was opened from). What was saved, or null.
    /// </summary>
    public async Task<Homestead.Details?> SaveBuilding(JsonObject clip, Homestead.Details suggested, string? openedFrom = null)
    {
        Status = FindHomestead();
        if (await Ask(suggested, clip, openedFrom != null ? Path.GetFileName(openedFrom) : null) is not { } details)
        {
            return null;
        }
        return SaveBuildingAs(clip, details) == null ? details : null;
    }

    /// <summary>
    /// A building written as a Homestead blueprint with these details, without asking (replacing one of that name),
    /// with its picture; Message tells what was done.
    /// </summary>
    /// <returns>Null when saved, else why not.</returns>
    internal string? SaveBuildingAs(JsonObject clip, Homestead.Details details)
    {
        Status = FindHomestead();
        try
        {
            WriteHomestead(clip, details, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            string problem = $"Could not save the blueprint: {ex.Message}";
            Message?.Invoke(problem);
            return problem;
        }
        int count = (clip["objects"] as JsonArray)?.Count ?? 0;
        Message?.Invoke($"Saved the blueprint “{details.Name}” ({count} piece(s)) for Homestead.{Reminder}");
        Refresh();
        return null;
    }

    /// <summary>
    /// Details…: a blueprint's name, description and tags, changed in its file (renamed with its
    /// picture when the name changes and the new file name is free).
    /// </summary>
    /// <returns>Whether the details were saved.</returns>
    public async Task<bool> EditDetails(Homestead.Entry e)
    {
        var d = await AskDetails(e.Details, Terrain.PieceCost.Of(e.Kinds).Describe(8));
        if (d == null || string.IsNullOrWhiteSpace(d.Name))
        {
            return false;
        }
        d = d with { Name = d.Name.Trim(), Description = d.Description.Trim() };
        try
        {
            string path = e.Path, target = Path.Combine(Path.GetDirectoryName(path)!, Homestead.FileName(d.Name));
            File.WriteAllText(path, Homestead.WithDetails(File.ReadAllText(path), d));
            if (!string.Equals(target, path, StringComparison.Ordinal) && !File.Exists(target))
            {
                File.Move(path, target);
                if (File.Exists(Path.ChangeExtension(path, ".png")))
                {
                    File.Move(Path.ChangeExtension(path, ".png"), Path.ChangeExtension(target, ".png"));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message?.Invoke($"Could not change the blueprint: {ex.Message}");
            return false;
        }
        Message?.Invoke($"“{d.Name}”: details saved.");
        Refresh();
        return true;
    }

    /// <summary>
    /// Loads a blueprint onto the clipboard and starts pasting. Object ids only mean something in the
    /// world the copy came from; kinds this world cannot make are left out.
    /// </summary>
    public void Paste(string id)
    {
        if (Load(id, out string? problem) is not var (clip, name, from, dungeon))
        {
            Message?.Invoke(problem ?? "That blueprint could not be read.");
            Refresh();
            return;
        }
        var world = _scene()?.World;
        CopyData data;
        try
        {
            data = CopyFormat.FromJson(clip, name, keepSources: world != null && from == world.Name);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            Message?.Invoke($"That blueprint could not be read: {ex.Message}.");
            Refresh();
            return;
        }
        data.Dungeon = dungeon;
        var missing = data.Objects.Where(o => world != null && !world.CanCreate(o.Prefab)).Select(o => o.Name).Distinct().ToList();
        data.Objects.RemoveAll(o => missing.Contains(o.Name));
        _paste.Clip = data;
        _paste.Notify();
        Card.IsVisible = false;
        Pasting?.Invoke();
        string how = dungeon ? "click where its entrance goes: a dungeon, it goes 5000 m above, behind the entrance of the biome there." : "click to place it.";
        Message?.Invoke($"“{name}” is on the clipboard: {how}{(missing.Count > 0 ? $" {missing.Count} kind(s) are unknown here and are left out: {string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? "…" : "")}." : "")}");
    }

    /// <summary>An older editor blueprint as a Homestead blueprint (objects only).</summary>
    /// <returns>The name it was saved under (numbered when taken), null when not saved.</returns>
    public string? ToHomestead(string id)
    {
        if (Load(id, out string? problem) is not var (clip, name, world, _))
        {
            Message?.Invoke(problem ?? "That blueprint could not be read.");
            return null;
        }
        string target = Homestead.FreeName(name, HomesteadExists);
        if (!TryWrite(() => WriteHomestead(clip, Named(target), world)))
        {
            return null;
        }
        Message?.Invoke($"“{target}” is now a Homestead blueprint.{(HasGround(clip) ? " Its ground shape is not kept: Homestead blueprints hold pieces only." : "")}{Reminder}");
        Refresh();
        return target;
    }

    /// <summary>Deletes a blueprint after asking: a Homestead one with its picture, or an older one from its store.</summary>
    public async Task Delete(string id, string name)
    {
        if (!await Confirm($"Delete the blueprint “{name}”? Its file is removed."))
        {
            return;
        }
        if (id.StartsWith(HomesteadId, StringComparison.Ordinal))
        {
            string path = Path.Combine(Status.Folder, id[HomesteadId.Length..]);
            if (Path.GetFileName(path) == id[HomesteadId.Length..] && !TryWrite(() =>
            {
                File.Delete(path);
                File.Delete(Path.ChangeExtension(path, ".png"));
            }))
            {
                return;
            }
        }
        else
        {
            Store.Delete(id);
        }
        Refresh();
    }

    /// <summary>Writes a blueprint for PlanBuild (or as .vbuild) into the export folder (objects only).</summary>
    /// <param name="id">The blueprint's id ("hs:" and its file name, or its store id).</param>
    /// <param name="format">The file extension and format: "blueprint" or "vbuild".</param>
    /// <returns>The file written, null when not.</returns>
    public string? Export(string id, string format)
    {
        if (Load(id, out string? problem) is not var (clip, name, _, _))
        {
            Message?.Invoke(problem ?? "Could not export that blueprint.");
            return null;
        }
        string text = BlueprintFormats.Write(clip, format, name, n => Terrain.PieceCatalog.Get(StableHash.Of(n))?.Category ?? 0);
        string dir = Path.Combine(Store.Directory, "export");
        string path = Path.Combine(dir, Path.GetFileNameWithoutExtension(Homestead.FileName(name)) + "." + format);
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message?.Invoke($"Could not write {path}: {ex.Message}");
            return null;
        }
        Message?.Invoke($"Written to {path}.{(format == "blueprint" ? " For PlanBuild, copy it into BepInEx/config/PlanBuild/blueprints." : "")} Only the objects are written, not the ground.");
        return path;
    }

    /// <summary>
    /// Other mods' blueprint files (PlanBuild .blueprint, .vbuild, Homestead's from elsewhere): read, then
    /// kept as a Homestead blueprint.
    /// </summary>
    /// <returns>The name it was saved under (numbered when taken), null when not imported.</returns>
    public string? Import(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message?.Invoke($"Could not import that file: {ex.Message}");
            return null;
        }
        var parsed = BlueprintFormats.Parse(Path.GetFileName(path), text);
        if (parsed.Pieces.Count == 0 && parsed.Terrain.Count == 0)
        {
            Message?.Invoke("Could not import that file: no pieces found in it. Is it a .blueprint (Homestead, PlanBuild) or a .vbuild file?");
            return null;
        }
        Status = FindHomestead();
        var world = _scene()?.World;
        JsonObject clip;
        List<string> unknown;
        try
        {
            clip = BlueprintFormats.ToClip(parsed, n => world?.CanCreate(StableHash.OfName(n)) ?? Terrain.PrefabCatalog.Get(StableHash.OfName(n)) != null, out unknown);
        }
        catch (InvalidDataException ex)
        {
            Message?.Invoke($"Could not import that file: {ex.Message}.");
            return null;
        }
        string name = Homestead.FreeName(string.IsNullOrWhiteSpace(parsed.Name) ? "Imported" : parsed.Name, HomesteadExists);
        if (!TryWrite(() => WriteHomestead(clip, new Homestead.Details(name, parsed.Description ?? "", Homestead.Details.ParseTags(TagsOf(text))), null)))
        {
            return null;
        }
        Refresh();
        int count = (clip["objects"] as JsonArray)?.Count ?? 0;
        Message?.Invoke($"Imported “{name}” as a Homestead blueprint: {count} piece(s)."
            + (parsed.Terrain.Count > 0 ? " Its terrain marks are not kept: Homestead blueprints hold pieces only." : "")
            + (unknown.Count > 0 ? $" {unknown.Count} kind(s) the game does not know were left out (mods?): {string.Join(", ", unknown.Take(5))}{(unknown.Count > 5 ? "…" : "")}." : "")
            + (parsed.SkippedLines > 0 ? $" {parsed.SkippedLines} unreadable line(s) skipped." : "") + Reminder);
        return name;
    }

    /// <summary>A blueprint file written or removed; when the folder refuses (read-only, locked), says so (false).</summary>
    private bool TryWrite(Action write)
    {
        try
        {
            write();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message?.Invoke($"Could not write in the blueprints folder: {ex.Message}");
            return false;
        }
    }

    /// <summary>The "#Tags:" line of a blueprint file (null when it has none).</summary>
    private static string? TagsOf(string text) => text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("#Tags:", StringComparison.OrdinalIgnoreCase))?[6..];
}
