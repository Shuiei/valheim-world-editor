namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The pictures around the tools: the 3D editor itself (overview, history, help, walking), Script, the world map,
/// the start page and its settings, and a new world.
/// </summary>
public sealed partial class Scenes
{
    /// <summary>overview.jpg: a base and a wood behind it, in the 3D editor with the View panel open.</summary>
    private void Overview(Editor e)
    {
        // The new pieces' green marks off: every piece of this base is unsaved, the dots would hide it.
        // The limit marks on, as by default (nothing here reaches the limit).
        Bare(e, view: true, unsaved: false, limit: true);
        BuildBase(e);
        Trees(e, P(-26, 26), P(26, 27));
        e.Send("panel view");
        Camera(e, 4, -4, 20, 42, 66);
        e.Shot(Out("overview.jpg"));
    }

    /// <summary>unsaved.jpg: levelled ground and new trees, with their unsaved marks and the status message.</summary>
    private void Unsaved(Editor e)
    {
        Bare(e);
        Level(e);
        Trees(e, P(-14, 0), P(14, 8), density: 5, kinds: ["Beech_small1", "Bush01", "RaspberryBush"]);
        Camera(e, 8, 2, 200, 48, 52);
        e.Shot(Out("unsaved.jpg"), keepMessage: true);
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

    /// <summary>help.jpg: the help (F3) over the bare zone.</summary>
    private void Help(Editor e)
    {
        Bare(e);
        e.Send("key F3");
        Camera(e, -12, 0, 200, 40, 60);
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
    /// map.jpg (the map with the bare zone picked), map-search.jpg (Search the world: every Beech, pinned) and
    /// map-reset.jpg (Reset zones across the world: the Meadows zones nobody built in, shown).
    /// </summary>
    private void Map(Editor e)
    {
        e.Send($"world {World}");
        e.Send($"mapview {Cx + 200} {Cz} 1.6");
        e.Send($"mappick {ZoneX} {ZoneZ}");
        e.Shot(Out("map.jpg"));
        e.Send("search Beech1");
        e.Send("wait 1500");
        e.Shot(Out("map-search.jpg"));
        e.Send("click Reset zones across the world");
        e.Send("click Meadows");
        e.Send("zones");
        e.Send($"mapview {Cx} {Cz} 6");
        e.Send("wait 1500");
        e.Shot(Out("map-reset.jpg"));
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
        e.Send("wait 4000");
        e.Shot(Out("live-applied.jpg"), keepMessage: true);
    }

    // ---- A new world (Create on the start page, no world opened).

    /// <summary>
    /// new-world.jpg (a seed's world before it exists: the start, the bosses' altars and the traders),
    /// new-world-dungeons.jpg (the same seed with the dungeons shown) and new-world-find.jpg (Find a seed's
    /// results).
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
        e.Shot(Out("new-world.jpg"));
        foreach (string d in new[] { "Burial chambers", "Troll caves", "Sunken crypts", "Frost caves" })
        {
            e.Send($"check {d}|on");
        }
        e.Send("wait 30000");
        e.Shot(Out("new-world-dungeons.jpg"));
        foreach (string d in new[] { "Burial chambers", "Troll caves", "Sunken crypts", "Frost caves" })
        {
            e.Send($"check {d}|off");
        }
        // 60 seeds with only Swamp and Mountain wished near: found in a few seconds, the found seeds under the search.
        e.Send("check The start on a large landmass|off");
        e.Send("check Land all around the start|off");
        e.Send("choose 60 seeds");
        e.Send("click Find seeds");
        e.Send("wait 60000");
        // Over the Find a seed card's text (a wheel over a number box would change its value).
        e.Scroll(240, 430, 20);
        // Again once the list has settled (the found seeds fill in as they are scored).
        e.Send("wait 2000");
        e.Scroll(240, 430, 20);
        e.Shot(Out("new-world-find.jpg"));
    }

    // ---- The start page and its settings (no world opened).

    /// <summary>
    /// The start page's pictures (start-home, start-game, start-server, start-offline, start-workshop), settings.jpg and
    /// settings-claude.jpg (Allow Claude to connect switched on); no world is opened.
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
        e.Send("click The Workshop");
        e.Send("wait 800");
        e.Shot(Out("start-workshop.jpg"));
        e.Send("click Settings");
        e.Send("wait 800");
        e.Shot(Out("settings.jpg"));
        e.Send("check Allow Claude to connect|on");
        e.Send("wait 500");
        e.Scroll(720, 600, 5);
        e.Shot(Out("settings-claude.jpg"));
        e.Send("click Cancel");
    }
}
