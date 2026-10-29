namespace TerrainEditor.Desktop;

/// <summary>
/// Where the documentation speaks of each part of the app: what F1 and the panels' help buttons (Ui.HelpButton) open
/// in the Help window. HelpTopicsTests checks that each one leads to a page and heading the app carries.
/// </summary>
public static class HelpTopics
{
    /// <summary>The start page: the help's first page, where getting started begins.</summary>
    public static HelpTarget StartPage { get; } = HelpTarget.Home;

    /// <summary>The world map.</summary>
    public static HelpTarget Map { get; } = new("map.md");

    /// <summary>A new world: a seed's world before it exists.</summary>
    public static HelpTarget NewWorld { get; } = new("new-world.md");

    /// <summary>The 3D editor's window: the top bar, the rail, the camera.</summary>
    public static HelpTarget Editor { get; } = new("editor-basics.md");

    /// <summary>The History panel.</summary>
    public static HelpTarget History { get; } = new("editor-basics.md", "history");

    /// <summary>The View panel.</summary>
    public static HelpTarget ViewPanel { get; } = new("editor-basics.md", "view-panel");

    /// <summary>The help card (the mouse and the keys).</summary>
    public static HelpTarget Controls { get; } = new("help/shortcuts.md");

    /// <summary>The Inspector: an object's data.</summary>
    public static HelpTarget Inspector { get; } = new("select.md", "inspector");

    /// <summary>The Mask card.</summary>
    public static HelpTarget Mask { get; } = new("masks.md");

    /// <summary>The Workshop and its Build panel.</summary>
    public static HelpTarget Workshop { get; } = new("workshop.md");

    /// <summary>The Blueprints panel (pasting a blueprint into a world).</summary>
    public static HelpTarget Blueprints { get; } = new("workshop.md", "blueprints-panel");

    /// <summary>Live mode: Apply live, Auto, Reload.</summary>
    public static HelpTarget Live { get; } = new("live-mode.md");

    /// <summary>Settings.</summary>
    public static HelpTarget Settings { get; } = new("editor-basics.md", "settings");

    /// <summary>A tool's page (a brush's part of the sculpt and paint page).</summary>
    /// <param name="mode">The tool.</param>
    /// <param name="brush">The brush, for <c>ToolMode.Brush</c>.</param>
    public static HelpTarget Tool(ToolMode mode, BrushTool? brush = null) => mode switch
    {
        ToolMode.Brush when brush is { } b && ToolPanel.IsPaint(b) => new("terrain.md", "paint-brushes"),
        ToolMode.Brush => new("terrain.md", "sculpt-brushes"),
        ToolMode.Select => new("select.md"),
        ToolMode.Measure => new("measure.md"),
        ToolMode.Shape => new("shape.md"),
        ToolMode.Path => new("path.md"),
        ToolMode.Area => new("area.md"),
        ToolMode.Paste => new("area.md", "copy-and-paste"),
        ToolMode.Place => new("place.md"),
        ToolMode.Mountain => new("mountain.md"),
        ToolMode.Script => new("scripting.md"),
        ToolMode.Blueprints => Blueprints,
        ToolMode.Dungeon => new("dungeons.md"),
        ToolMode.Mod => new("editor-basics.md", "the-tool-rail"),
        _ => new("editor-basics.md", "camera"),
    };

    /// <summary>Every place above, each tool's included (HelpTopicsTests).</summary>
    public static IEnumerable<HelpTarget> All =>
        [StartPage, Map, NewWorld, Editor, History, ViewPanel, Controls, Inspector, Mask, Workshop, Blueprints, Live, Settings,
            .. Enum.GetValues<ToolMode>().Select(m => Tool(m)), .. Enum.GetValues<BrushTool>().Select(b => Tool(ToolMode.Brush, b))];
}
