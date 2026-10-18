
namespace TerrainEditor.Desktop;

/// <summary>
/// The rest of what the driver's state answers with: the map, the models and the start page.
/// </summary>
public static partial class Driver
{
    /// <summary>
    /// Adds the map's, the models' and the start page's fields to what State answers with, in their order.
    /// </summary>
    /// <param name="w">The window.</param>
    /// <param name="fields">The fields so far.</param>
    private static void AddMapFields(MainWindow w, Dictionary<string, object?> fields)
    {
        fields["mapX"] = w.MapPage?.Map.Center.X ?? 0;
        fields["mapScale"] = w.MapPage?.Map.MetersPerPixel ?? 0;
        fields["pins"] = w.MapPage?.Map.Pins.Count ?? 0;
        fields["matches"] = w.MapPage?.Matches.Count ?? 0;
        fields["detail"] = w.MapPage?.Map.DetailShown ?? false;
        fields["models"] = w.View.Models != null;
        // The game's look copied from Valheim and in use (else plain colours: no Valheim, as in CI).
        fields["gameLook"] = w.View.GameLookLoaded;
        fields["modelParts"] = w.View.LastStats?.Instances ?? 0;
        fields["modelsLoading"] = w.View.LastStats?.PendingModels ?? 0;
        // The start page's game look banner: "running" while the game's files are read.
        fields["look"] = w.StartPage?.LookBanner.Shown ?? "";
        // The Place tool at the last press: what is chosen, its mode, and what that press did.
        fields["placeChosen"] = w.View.Place is { } place ? string.Join(",", place.Tool.Chosen) : "";
        fields["placeMode"] = w.View.Place?.Tool.Mode.ToString() ?? "";
        fields["placePress"] = w.View.Place?.LastPress ?? "";
    }
}
