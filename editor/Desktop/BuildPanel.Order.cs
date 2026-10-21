using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>
    /// The mod a piece of the Mods tab is listed under ("Other mods" when the game did not say); empty for the game's
    /// own pieces. A mod that adds many (PlanBuild's plans of every piece) does not bury the others.
    /// </summary>
    internal static string ModGroup(string prefab) => ModCatalogue.IsModded(prefab) ? ModCatalogue.ModOf(prefab) ?? Lang.T("build.otherMods") : "";

    /// <summary>
    /// The group a mod's manifest puts a mod piece in (the Mods tab's heading in place of its station); null for the
    /// game's own pieces and when the manifest says none.
    /// </summary>
    internal static string? PieceGroup(string prefab) => ModCatalogue.IsModded(prefab) ? ModManifests.PrefabOf(prefab)?.Group : null;

    /// <summary>
    /// A station's rank, to list pieces by the station they need, in the order players get them (none first:
    /// built anywhere; one not listed last), then by name.
    /// </summary>
    internal static int StationRank(string? station) => station?.ToLowerInvariant() switch
    {
        null or "" => 0,
        "workbench" => 1,
        "stonecutter" => 2,
        "forge" => 3,
        "artisan table" => 4,
        "black forge" => 5,
        "galdr table" => 6,
        _ => 7,
    };
}
