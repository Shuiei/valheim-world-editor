using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The pictures around the tools: the 3D editor itself (overview, history, help, walking), Script, the world map,
/// the start page and its settings, and a new world.
/// </summary>
public sealed partial class Scenes
{
    /// <summary>
    /// overview.jpg: a base in a 3 x 3 area, Raise chosen (its panel beside the tool rail), the View panel open; the
    /// window tall enough for the whole rail (Program.cs), the pointer on the ground for the status bar's readout.
    /// </summary>
    private void Overview(Editor e)
    {
        // The new pieces' green marks off: every piece of this base is unsaved, the dots would hide it.
        // The limit marks on, as by default (nothing here reaches the limit).
        Bare(e, view: true, unsaved: false, limit: true, size: 3);
        BuildBase(e);
        e.Send("key Escape");
        e.Send("key D1");
        e.Send("set Size|8");
        e.Send("set Strength|0.4");
        e.Send("panel view");
        // From the south, a little west of the base's middle: it sits between the two panels.
        Camera(e, -6, 2, 20, 64, 78);
        Move(e, P(24, 34));
        e.Send("wait 1000");
        e.Shot(Out("overview.jpg"));
    }

    /// <summary>
    /// unsaved.jpg: a few trees just planted on levelled ground, close up, each with its green unsaved mark, and the
    /// top bar's pending line.
    /// </summary>
    private void Unsaved(Editor e)
    {
        Bare(e);
        Level(e);
        // Young beeches, one at a time where clicked (the same places each time, for the inset).
        e.Send("key T");
        Kinds(e, "Beech_small1", "Beech_small2");
        Nature(e);
        foreach (var (dx, dz) in new[] { (-8, -2), (-3, 3), (2, -3), (7, 2), (12, -2), (-6, 8), (4, 9), (10, 8), (-1, -8), (8, -7) })
        {
            Single(e, P(dx, dz));
        }
        e.Send("key Escape");
        Camera(e, 2, 1, 200, 54, 24);
        e.Send("wait 1000");
        string shot = Out("unsaved.jpg");
        e.Shot(shot);
        // The dots are a few pixels at any distance: one tree's foot, four times as large, in the corner.
        Inset(shot, shot, NearestDot(shot), new SKRectI(1150, 600, 1411, 819));
    }

    /// <summary>history.jpg: the History panel after a few edits, one step undone with Back to here.</summary>
    private void History(Editor e)
    {
        Bare(e);
        Stroke(e, "D1", P(-8, 0), 8, hold: 1200, steps: 3);
        Stroke(e, "D1", P(8, 4), 8, hold: 1200, steps: 3);
        Stroke(e, "D2", P(0, -10), 6, hold: 1000, steps: 3);
        e.Send("key D6");
        e.Send("set Size|3");
        Path(e, [P(-16, 12), P(16, 12)], steps: 6);
        Stroke(e, "D4", P(0, 0), 10, hold: 1500, steps: 3);
        e.Send("key Escape");
        e.Send("click History");
        e.Send("click Back to here");
        Camera(e, -10, 0, 200, 40, 58);
        e.Shot(Out("history.jpg"));
    }

    /// <summary>
    /// help.jpg: the Controls card (F3) over a 3 x 3 area, scrolled to its end: the last keys and This computer.
    /// </summary>
    private void Help(Editor e)
    {
        Bare(e, size: 3);
        Camera(e, -40, 0, 200, 40, 170);
        e.Send("key F3");
        // The wheel over the card (right of the window), down to its end.
        e.Scroll(1250, 500, 30);
        e.Shot(Out("help.jpg"));
    }

    /// <summary>walk.jpg: the base seen as a player sees it, walking (F) at eye height.</summary>
    private void Walk(Editor e)
    {
        Bare(e, unsaved: false);
        BuildBase(e);
        Trees(e, P(-26, 26), P(26, 27));
        // Inside the palisade, by the field, facing the house (Walk starts where the view looks, facing its way).
        Camera(e, 10, -12, 32, 10, 12);
        e.Send("key F");
        e.Send("wait 1500");
        e.Shot(Out("walk.jpg"), keepMessage: true);
    }

    /// <summary>script.jpg: the Terraced hill example script, run on a 3 x 3 area.</summary>
    private void Script(Editor e)
    {
        Bare(e, size: 3);
        e.Send("click Script");
        e.Send("choose Example: Terraced hill");
        e.Send("click Run");
        // Compiling the first script takes a moment.
        e.Send("wait 8000");
        Camera(e, 40, 0, 200, 34, 190);
        e.Send("wait 1500");
        e.Shot(Out("script.jpg"), keepMessage: true);
    }

    /// <summary>
    /// save-confirm.jpg: a raised hill and young trees pending in a saved world, Save to world pressed: the question
    /// before anything is written (answered Cancel). The world is opened from its card, in Valheim's own worlds folder
    /// (Program.cs links it there), so its folder reads "~/…".
    /// </summary>
    private void SaveConfirm(Editor e)
    {
        OpenSaved(e);
        Stroke(e, "D1", P(-4, 2), 10, hold: 6000, steps: 3);
        e.Send("key T");
        Kinds(e, "Beech_small1", "Beech_small2");
        Nature(e);
        foreach (var (dx, dz) in new[] { (8, -6), (12, 0), (10, 8), (16, 4), (4, -12), (14, -10), (-14, 12), (-18, 4), (-16, -6), (-8, -14) })
        {
            Single(e, P(dx, dz));
        }
        e.Send("key Escape");
        // The hill and the trees below the question (north, near the camera, is low on the picture).
        Camera(e, 0, -14, 200, 34, 46);
        e.Send("click Save to world");
        e.Send("wait 1000");
        e.Shot(Out("save-confirm.jpg"));
        e.Send("click Cancel");
    }

    /// <summary>
    /// The Docs world opened as a player opens it: A saved world, then its card (the world linked in Valheim's own
    /// folder); then the bare zone in the 3D editor with the sea and the red limit marks off, as <c>Bare</c> has it.
    /// </summary>
    /// <param name="e">The editor.</param>
    /// <param name="size">The area's width in zones.</param>
    private static void OpenSaved(Editor e, int size = 1)
    {
        e.Send("click A saved world");
        e.Send("wait 1500");
        e.Send("click Docs");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (e.Send("wait 500").GetProperty("page").GetString() != "map" && watch.Elapsed < TimeSpan.FromMinutes(1))
        {
        }
        e.Send($"area {ZoneX} {ZoneZ} {size}");
        e.CloseLocationNote();
        e.Send("click Water");
        e.Send("click Limit marks");
        e.Send("click View");
    }

    /// <summary>
    /// script-error.jpg: a short script with a misspelt name on line 2, run on a 3 x 3 area: the Output's mistakes and the
    /// status bar's message.
    /// </summary>
    private void ScriptError(Editor e)
    {
        Bare(e, size: 3);
        e.Send("click Script");
        e.Send("wait 800");
        TypeAt(e, 370, 300, "// A hill in the middle.\nGroud.Raise(Area.CenterX, Area.CenterZ, 5);\n");
        e.Send("click Run");
        e.Send("wait 8000");
        Camera(e, 40, 0, 200, 34, 190);
        e.Send("wait 1000");
        e.Shot(Out("script-error.jpg"), keepMessage: true);
    }

    /// <summary>
    /// map.jpg (the map with the bare zone picked) and map-search.jpg (Search the world: every Beech pinned, one picked
    /// in the list, ringed in gold and centred).
    /// </summary>
    private void Map(Editor e)
    {
        e.Send($"world {World}");
        e.Send($"mapview {Cx + 200} {Cz} 1.6");
        e.Send($"mappick {ZoneX} {ZoneZ}");
        e.Shot(Out("map.jpg"));
        e.Send("search Beech1");
        e.Send("wait 1500");
        e.Send("hit 3");
        e.Send("wait 1500");
        e.Shot(Out("map-search.jpg"));
    }

    /// <summary>
    /// map-reset.jpg: Reset zones across the world, the Meadows zones marked for reset (red, asked first), then the
    /// Black Forest ones shown matching (blue), the side panel scrolled down to the count line under the buttons.
    /// </summary>
    private void MapReset(Editor e)
    {
        e.Send($"world {World}");
        e.Send("click Search the world");
        e.Send("click Reset zones across the world");
        e.Send("click Meadows");
        e.Send("click Show matching");
        e.Send("click Mark for reset…");
        e.Send("wait 500");
        e.Send("click Yes");
        e.Send("click Meadows");
        e.Send("click Black Forest");
        e.Send("click Show matching");
        e.Send($"mapview {Cx} {Cz} 6");
        e.Send("wait 1500");
        // The side panel scrolled down to the count line.
        e.Scroll(170, 600, 20);
        e.Shot(Out("map-reset.jpg"));
    }

    /// <summary>
    /// map-edited-zones.jpg: the map close in on a copy of the Docs world where four zones were edited and saved
    /// (yellow) and two more edited since (orange, pending), the Edited zones list open.
    /// </summary>
    private void MapEditedZones(Editor e)
    {
        string copy = CopyWorld();
        try
        {
            e.Send($"world {copy}");
            e.Send($"area {ZoneX} {ZoneZ} 3");
            e.CloseLocationNote();
            e.Send("click View");
            // Saved: ground raised in four zones, one also painted.
            foreach (var (dx, dz) in new[] { (0, 0), (64, 0), (0, 64), (-64, -64) })
            {
                Stroke(e, "D1", P(dx, dz), 10, hold: 1500, steps: 3);
            }
            Stroke(e, "D6", P(10, 6), 6, hold: 800, steps: 3);
            e.Send("click Save to world");
            e.Send("wait 800");
            e.Send("click Save");
            e.Send("wait 4000");
            e.Send("click OK");
            // Pending: two more zones.
            foreach (var (dx, dz) in new[] { (-64, 0), (64, 64) })
            {
                Stroke(e, "D1", P(dx, dz), 10, hold: 1500, steps: 3);
            }
            e.Send("map");
            e.Send("wait 1500");
            e.Send("click Edited zones");
            e.Send($"mapview {Cx - 60} {Cz} 0.45");
            e.Send("wait 1500");
            e.Scroll(170, 600, 30);
            e.Shot(Out("map-edited-zones.jpg"));
        }
        finally
        {
            Directory.Delete(System.IO.Path.GetDirectoryName(copy)!, recursive: true);
        }
    }

    /// <summary>
    /// game-saved-replay.jpg: a copy of the Docs world with three changes pending when the game saves it again (a save
    /// #2 written beside the editor's #1, as Valheim writes it), then Save to world: the question offering to bring the
    /// changes over.
    /// </summary>
    private void GameSavedReplay(Editor e)
    {
        string copy = CopyWorld();
        try
        {
            e.Send($"world {copy}");
            e.Send($"area {ZoneX} {ZoneZ} 1");
            e.CloseLocationNote();
            e.Send("click Water");
            e.Send("click Limit marks");
            e.Send("click View");
            Stroke(e, "D1", P(-8, 0), 8, hold: 1500, steps: 3);
            Stroke(e, "D2", P(8, 4), 8, hold: 1200, steps: 3);
            Stroke(e, "D6", P(0, -10), 6, hold: 800, steps: 3);
            e.Send("key Escape");
            // The game saves the world meanwhile: its save #2, the same as #1.
            foreach (string f in Directory.GetFiles(copy))
            {
                string name = System.IO.Path.GetFileName(f);
                string next = name.EndsWith("__0_1.chunk", StringComparison.Ordinal) ? name.Replace("__0_1.chunk", "__0_2.chunk", StringComparison.Ordinal)
                    : name.Replace("_main.1.", "_main.2.", StringComparison.Ordinal);
                File.Copy(f, System.IO.Path.Combine(copy, next));
            }
            Camera(e, 0, 0, 200, 40, 80);
            e.Send("click Save to world");
            e.Send("wait 800");
            e.Send("click Save");
            e.Send("wait 3000");
            e.Shot(Out("game-saved-replay.jpg"));
            e.Send("click Not now");
        }
        finally
        {
            Directory.Delete(System.IO.Path.GetDirectoryName(copy)!, recursive: true);
        }
    }

    /// <summary>A copy of the Docs world in a new temporary folder (named Docs, as the world), to save into.</summary>
    /// <returns>The copy's folder.</returns>
    private string CopyWorld()
    {
        string copy = System.IO.Path.Combine(Directory.CreateTempSubdirectory("vwe-docs-world-").FullName, "Docs");
        Directory.CreateDirectory(copy);
        foreach (string f in Directory.GetFiles(World))
        {
            File.Copy(f, System.IO.Path.Combine(copy, System.IO.Path.GetFileName(f)));
        }
        return copy;
    }

    /// <summary>
    /// concept-area-zones.jpg: a 5 x 5 area seen straight down with the zone borders, labelled after the shot: one zone,
    /// the area, its locked edge (the outermost metre) and the line past which Follow grows it (12 m inside).
    /// </summary>
    private void ConceptAreaZones(Editor e)
    {
        Bare(e, size: 5, unsaved: false);
        Camera(e, 0, 0, 180, 89, 400);
        e.Send("wait 2000");
        string shot = Out("concept-area-zones.jpg");
        e.Shot(shot);
        AnnotateAreaZones(shot);
    }

    /// <summary>
    /// claude-history.jpg: a 3 x 3 area after Claude (called over its connection, as Claude Code calls it) flattened a
    /// square, then paved it and planted a ring of trees round it, with a Raise of the user's own in between: the
    /// History panel with Claude's rows tagged and Take back Claude's changes. Port 5731 must be free (see Settings).
    /// </summary>
    private void ClaudeHistory(Editor e)
    {
        WaitForLook(e);
        e.Send("click Settings");
        e.Send("wait 800");
        e.Send("check Allow Claude to connect|on");
        e.Send("click Save");
        e.Send("wait 2000");
        Bare(e, size: 3, unsaved: false);
        string token = ClaudeToken(e);
        ClaudeCall(token, "flatten", new { x0 = Cx - 10, z0 = Cz - 10, x1 = Cx + 10, z1 = Cz + 10 });
        Stroke(e, "D1", P(14, 14), 8, hold: 2000, steps: 3);
        e.Send("key Escape");
        ClaudeCall(token, "paint_area", new { kind = "paved", x0 = Cx - 8, z0 = Cz - 8, x1 = Cx + 8, z1 = Cz + 8 });
        ClaudeCall(token, "forest", new { x0 = Cx - 18, z0 = Cz - 18, x1 = Cx + 18, z1 = Cz + 18, kinds = YoungBeeches, spacing = 5 });
        e.Send("click History");
        Camera(e, 10, 4, 200, 62, 62);
        e.Send("wait 1500");
        e.Shot(Out("claude-history.jpg"));
    }

    /// <summary>The young beeches Claude plants in claude-history.jpg.</summary>
    private static readonly string[] YoungBeeches = ["Beech_small1", "Beech_small2"];

    /// <summary>The token Claude must send, as Settings saved it in the stand-in home.</summary>
    private static string ClaudeToken(Editor e)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(e.DataFolder, "settings.json")));
        return doc.RootElement.GetProperty("ClaudeToken").GetString() ?? throw new InvalidOperationException("no Claude token in the settings");
    }

    /// <summary>
    /// One of the editor's tools called as Claude calls it: a JSON-RPC tools/call to its MCP address (port 5731), with
    /// the token. Throws when the call is refused or the tool reports an error.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">Its arguments.</param>
    private static void ClaudeCall(string token, string tool, object arguments)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5731/mcp")
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method = "tools/call", @params = new { name = tool, arguments } }), System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Authorization", "Bearer " + token);
        request.Headers.Add("Accept", "application/json, text/event-stream");
        using var response = http.Send(request);
        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode || body.Contains("\"isError\":true", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{tool}: {(int)response.StatusCode} {body}");
        }
        Console.WriteLine($"{tool}: {body[..Math.Min(300, body.Length)]}");
    }

    // ---- Live mode (a local server running the Docs world with the plugin, $DOCS_LIVE).

    /// <summary>
    /// live.jpg (the 3D editor connected to a running server: LIVE, Auto, a hut not applied yet) and live-applied.jpg
    /// (the same after Apply live: in the game at once).
    /// </summary>
    private void Live(Editor e)
    {
        // Connected at start (--live): the world's map, then the bare zone.
        e.Send("wait 3000");
        e.Send($"area {ZoneX} {ZoneZ} 1");
        e.CloseLocationNote();
        foreach (string sw in new[] { "Water", "Limit marks" })
        {
            e.Send($"click {sw}");
        }
        e.Send("click View");
        Level(e, P(-12, -10), P(12, 10));
        Hut(e, P(-4, -3), P(4, 3));
        e.Send("key Escape");
        e.Send("key H");
        Camera(e, 8, 0, 200, 34, 40);
        e.Send("wait 500");
        e.Shot(Out("live.jpg"), keepMessage: true);
        e.Send("click Apply live");
        // Taken as soon as the apply's result shows, before the game's next news replaces it.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!(e.Send("wait 200").GetProperty("message").GetString() ?? "").StartsWith("Applied", StringComparison.Ordinal) && watch.Elapsed < TimeSpan.FromSeconds(30))
        {
        }
        e.Shot(Out("live-applied.jpg"), keepMessage: true);
    }

    // ---- A new world (Create on the start page, no world opened).

    /// <summary>
    /// new-world.jpg (a seed's world before it exists: the start, the bosses' altars and the traders, the facts down to
    /// the bosses), new-world-dungeons.jpg (the same seed with four kinds of dungeon shown and their list) and
    /// new-world-find.jpg (Find a seed's wishes and the best seeds found, the first shown on the large map). The window is
    /// taller (Program.cs) and the map zoomed out and moved down (two wheel notches), so the switches over its top right corner leave the
    /// whole world in view.
    /// </summary>
    private void NewWorld(Editor e)
    {
        WaitForLook(e);
        e.Send("click A new world");
        e.Send("click Make a new world");
        e.Send("wait 1500");
        e.Send("type My world|Fjordheim");
        e.Send("type Up to 10|5DCcdIcuYJ");
        e.Send("click Generate world");
        e.Send("wait 15000");
        e.Send("click −");
        e.Send("wait 1000");
        // Moved down (the map cannot be dragged from here): in a notch at its top, out a notch at its bottom.
        WheelAt(e, (795, 75, 1), (795, 1070, -1));
        // The facts down to the bosses' list.
        e.Scroll(1250, 600, 4);
        e.Send("wait 1500");
        e.Shot(Out("new-world.jpg"));
        foreach (string d in new[] { "Burial chambers", "Troll caves", "Sunken crypts", "Frost caves" })
        {
            e.Send($"check {d}|on");
        }
        e.Send("wait 30000");
        // The facts down to the dungeons' list (the wheel over the facts column).
        e.Scroll(1250, 600, 20);
        e.Shot(Out("new-world-dungeons.jpg"));
        foreach (string d in new[] { "Burial chambers", "Troll caves", "Sunken crypts", "Frost caves" })
        {
            e.Send($"check {d}|off");
        }
        e.Scroll(1250, 600, -20);
        e.Send("choose 150 seeds");
        e.Send("click Find seeds");
        e.Send("wait 150000");
        // The Find a seed card's heading to the top (the wheel over its text: over a number box it would change it).
        e.Scroll(240, 430, 7);
        e.Send("wait 1000");
        ClickAt(e, FirstFoundSeed);
        // Its world drawn on the large map.
        e.Send("wait 20000");
        e.Shot(Out("new-world-find.jpg"));
    }

    // ---- The start page and its settings (no world opened).

    /// <summary>
    /// The start page's pictures (start-home, start-game, start-server, start-offline; start-workshop is the Workshop's,
    /// Scenes.Building.cs); no world is opened.
    /// </summary>
    private void Start(Editor e)
    {
        // Home: the running game (the stand-in plugin) and the worlds opened before, once the game's
        // look is read (its bar would be in every picture).
        WaitForLook(e);
        e.Shot(Out("start-home.jpg"));
        e.Send("click My game");
        e.Send("wait 1500");
        e.Shot(Out("start-game.jpg"));
        e.Send("click A dedicated server");
        e.Shot(Out("start-server.jpg"));
        e.Send("click A saved world");
        e.Send("wait 1500");
        e.Shot(Out("start-offline.jpg"));
    }

    /// <summary>
    /// start-valheim-missing.jpg: Home on a computer where Valheim is not found (Program.cs gives this scene a stand-in
    /// home without it): the card that asks for the game's folder.
    /// </summary>
    private void StartMissing(Editor e)
    {
        e.Send("wait 3000");
        e.Shot(Out("start-valheim-missing.jpg"));
    }

    /// <summary>
    /// start-game-setup.jpg: My game where Valheim has no BepInEx yet: the three steps to install it and the plugin.
    /// </summary>
    private void StartSetup(Editor e)
    {
        WaitForLook(e);
        e.Send("click My game");
        e.Send("wait 1500");
        e.Shot(Out("start-game-setup.jpg"));
    }

    /// <summary>
    /// settings.jpg (Settings from Language down to History and Save, Claude off: its top and its end side by side)
    /// and settings-claude.jpg (Claude switched on and listening, its section beside the end of Settings, the token
    /// and the program's path replaced after the shot). The app runs from the stand-in home (Program.cs), so the
    /// folders it names read "~". Port 5731 must be free: run it with no other editor allowing Claude, or in a
    /// network of its own (unshare -rn, lo up).
    /// </summary>
    private void Settings(Editor e)
    {
        WaitForLook(e);
        e.Send("click Settings");
        e.Send("wait 800");
        string top = Out("settings-top.tmp.jpg"), end = Out("settings-end.tmp.jpg");
        e.Shot(top);
        // The wheel over the dialog's text (its own window: the pointer is placed in it).
        e.Scroll(300, 400, 40);
        e.Shot(end);
        SideBySide(Out("settings.jpg"), top, end);
        // Claude allowed, saved (it starts listening), and Settings opened again to show it listening.
        e.Send("check Allow Claude to connect|on");
        e.Send("check Claude may run scripts|on");
        e.Send("click Save");
        e.Send("wait 2000");
        e.Send("click Settings");
        e.Send("wait 1000");
        e.Scroll(300, 400, ClaudeNotches);
        string claude = Out("settings-claude.tmp.jpg");
        e.Shot(claude);
        e.Scroll(300, 400, 40);
        e.Shot(end);
        e.Send("click Cancel");
        // The token and the program's path (the stand-in's) written over: what each reader sees is their own.
        Retext(claude, ClaudeSecrets);
        Retext(end, EndSecrets);
        SideBySide(Out("settings-claude.jpg"), claude, end);
        foreach (string f in new[] { top, end, claude })
        {
            File.Delete(f);
        }
    }
}
