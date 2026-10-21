using System.Text.Json;
using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>The test fixtures: files beside the tests, and the game's hoe strokes as the editor hands them over.</summary>
internal static class Fixtures
{
    /// <summary>The fixtures' folder.</summary>
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "fixtures");

    /// <summary>
    /// The game's TerrainOp components as the editor reads them (whole numbers as long, the rest as double), from
    /// terrain-ops.json: each stroke's settings by its object's name.
    /// </summary>
    public static List<GameComponent> TerrainOpComponents()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "terrain-ops.json")));
        return [.. doc.RootElement.EnumerateObject().Select(p => new GameComponent(p.Name, p.Name, new Dictionary<string, object?>
        {
            ["m_settings"] = p.Value.EnumerateObject().ToDictionary(f => f.Name, f => (object?)(f.Value.TryGetInt64(out long l) ? l : f.Value.GetDouble())),
        }))];
    }

    /// <summary>The game's hoe strokes by name hash, as the add-on reads them from the editor.</summary>
    public static Dictionary<int, HoeSettings> TerrainOps() => WorldEditor.TerrainOps.From(TerrainOpComponents());
}
