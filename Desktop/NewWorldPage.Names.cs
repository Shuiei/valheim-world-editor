using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The names and colours of the dungeon kinds and landmarks, as the user reads them.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// Each dungeon kind's colour (squares, apart from the round traders and the bosses' diamonds).
    /// </summary>
    private static SolidColorBrush DungeonFill(string name) => new(name switch
    {
        "Burial chambers" => Avalonia.Media.Color.FromRgb(226, 206, 156),
        "Troll caves" => Avalonia.Media.Color.FromRgb(64, 170, 150),
        "Sunken crypts" => Avalonia.Media.Color.FromRgb(160, 116, 78),
        "Frost caves" => Avalonia.Media.Color.FromRgb(132, 210, 255),
        "Infested mines" => Avalonia.Media.Color.FromRgb(104, 120, 255),
        "Charred fortresses" => Avalonia.Media.Color.FromRgb(255, 126, 44),
        _ => Avalonia.Media.Color.FromRgb(255, 176, 214),
    });

    /// <summary>
    /// A dungeon kind's name as the user reads it (<see cref="SeedPreview.Dungeons"/> names are ids, in English): its
    /// catalogue text, or the id itself for a kind not named here.
    /// </summary>
    /// <param name="name">The kind's name in <see cref="SeedPreview.Dungeons"/>.</param>
    internal static string DungeonName(string name)
    {
        return DungeonKeys.TryGetValue(name, out string? key) ? Lang.T(key) : name;
    }

    /// <summary>
    /// The catalogue key of each dungeon kind's name, by its id in <see cref="SeedPreview.Dungeons"/>.
    /// </summary>
    private static readonly Dictionary<string, string> DungeonKeys = new(StringComparer.Ordinal)
    {
        ["Burial chambers"] = "newWorld.dungeon.burialChambers",
        ["Troll caves"] = "newWorld.dungeon.trollCaves",
        ["Sunken crypts"] = "newWorld.dungeon.sunkenCrypts",
        ["Frost caves"] = "newWorld.dungeon.frostCaves",
        ["Infested mines"] = "newWorld.dungeon.infestedMines",
        ["Charred fortresses"] = "newWorld.dungeon.charredFortresses",
        ["Hildir's dungeons"] = "newWorld.dungeon.hildirs",
    };
    /// <summary>
    /// A landmark's name as the user reads it (<see cref="SeedPreview.Landmarks"/> names are ids, in English): its
    /// catalogue text, or the id itself for one not named here.
    /// </summary>
    /// <param name="name">The landmark's name in <see cref="SeedPreview.Landmarks"/>.</param>
    internal static string LandmarkName(string name)
    {
        return LandmarkKeys.TryGetValue(name, out string? key) ? Lang.T(key) : name;
    }

    /// <summary>
    /// The catalogue key of each landmark's name, by its id in <see cref="SeedPreview.Landmarks"/>.
    /// </summary>
    private static readonly Dictionary<string, string> LandmarkKeys = new(StringComparer.Ordinal)
    {
        ["The start"] = "newWorld.label.start",
        ["Haldor"] = "newWorld.landmark.haldor",
        ["The Bog Witch"] = "newWorld.landmark.bogWitch",
        ["Hildir"] = "newWorld.landmark.hildir",
        ["Eikthyr"] = "newWorld.landmark.eikthyr",
        ["The Elder"] = "newWorld.landmark.elder",
        ["Bonemass"] = "newWorld.landmark.bonemass",
        ["Moder"] = "newWorld.landmark.moder",
        ["Yagluth"] = "newWorld.landmark.yagluth",
        ["The Queen"] = "newWorld.landmark.queen",
        ["Fader"] = "newWorld.landmark.fader",
    };
}
