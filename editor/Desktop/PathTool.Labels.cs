using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    /// <summary>The roof choice as the panel lists it, in the language in use.</summary>
    public static string Label(CaveRock r) => r switch
    {
        CaveRock.Auto => Lang.T("cave.rock.auto"),
        CaveRock.Forest => Lang.T("cave.rock.forest"),
        CaveRock.Coast => Lang.T("cave.rock.coast"),
        CaveRock.Heath => Lang.T("cave.rock.heath"),
        _ => Lang.T("cave.rock.mountain"),
    };

    /// <summary>A cave preset the panel offers.</summary>
    /// <param name="Name">The preset as listed.</param>
    /// <param name="Width">Width in metres.</param>
    /// <param name="Soft">Soft edge in metres: the walls' slope.</param>
    /// <param name="Depth">Depth below the ground in metres.</param>
    /// <param name="Headroom">Headroom inside in metres.</param>
    /// <param name="Help">The line the panel shows under the preset.</param>
    public sealed record CavePreset(string Name, float Width, float Soft, float Depth, float Headroom, string Help);

    /// <summary>Tunnel, Cave and Cavern, as the panel lists them, named in the language in use.</summary>
    public static CavePreset[] CavePresets() =>
    [
        new(Lang.T("cave.preset.tunnel"), 5, 1.5f, 6, 3.5f, Lang.T("cave.preset.tunnelHelp")),
        new(Lang.T("cave.preset.cave"), 8, 2.5f, 7.5f, 4.5f, Lang.T("cave.preset.caveHelp")),
        new(Lang.T("cave.preset.cavern"), 14, 3.5f, 8, 5.5f, Lang.T("cave.preset.cavernHelp")),
    ];

    /// <summary>
    /// The action as the panel lists it, in both languages: the panel, the status line and the history show it in the
    /// language in use, Claude is told it in English.
    /// </summary>
    public static Said Label(Action a) => a switch
    {
        Action.Flatten => Said.Of("path.action.flatten"),
        Action.Ramp => Said.Of("path.action.ramp"),
        Action.Raise => Said.Of("path.action.raise"),
        Action.Lower => Said.Of("path.action.lower"),
        Action.Smooth => Said.Of("brush.label.smooth"),
        Action.River => Said.Of("path.action.river"),
        Action.Cave => Said.Of("path.action.cave"),
        Action.PaintDirt => Said.Of("path.action.paintDirt"),
        Action.PaintPaved => Said.Of("path.action.paintPaved"),
        Action.PaintCultivated => Said.Of("path.action.paintCultivated"),
        _ => Said.Of("brush.label.clear"),
    };
}
