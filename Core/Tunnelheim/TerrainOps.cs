using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Tunnelheim;

/// <summary>
/// The game's terrain operations' settings (the hoe's and the cultivator's strokes) by the hash of their object's
/// name, as the game's ObjectDB.TryGetTerrainOp finds them: a <see cref="DigKind.Hoe"/> edit names its stroke so, and
/// is replayed with the settings of the game it is loaded in. From the "terrain-ops" game data (GameData).
/// </summary>
public static class TerrainOps
{
    private static volatile IReadOnlyDictionary<int, HoeSettings> _byHash = new Dictionary<int, HoeSettings>();

    /// <summary>The settings by name hash, as last taken.</summary>
    public static IReadOnlyDictionary<int, HoeSettings> ByHash => _byHash;

    /// <summary>Takes the "terrain-ops" JSON: each operation's settings by its object's name, fields named as the game's.</summary>
    /// <param name="json">The JSON.</param>
    public static void Use(string json) => _byHash = Read(json);

    /// <summary>The settings by name hash, from the "terrain-ops" JSON.</summary>
    /// <param name="json">The JSON.</param>
    internal static Dictionary<int, HoeSettings> Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var byHash = new Dictionary<int, HoeSettings>();
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            var s = p.Value;
            byHash[StableHash.Of(p.Name)] = new HoeSettings(
                LevelOffset: F(s, "m_levelOffset"),
                Level: B(s, "m_level"),
                LevelRadius: F(s, "m_levelRadius"),
                Square: B(s, "m_square"),
                Raise: B(s, "m_raise"),
                RaiseRadius: F(s, "m_raiseRadius"),
                RaisePower: F(s, "m_raisePower"),
                RaiseDelta: F(s, "m_raiseDelta"),
                Smooth: B(s, "m_smooth"),
                SmoothRadius: F(s, "m_smoothRadius"),
                SmoothPower: F(s, "m_smoothPower"),
                PaintCleared: B(s, "m_paintCleared"),
                PaintType: s.GetProperty("m_paintType").GetInt32(),
                PaintRadius: F(s, "m_paintRadius"));
        }
        return byHash;
    }

    private static float F(JsonElement e, string name) => (float)e.GetProperty(name).GetDouble();

    private static bool B(JsonElement e, string name) => e.GetProperty(name).ValueKind == JsonValueKind.True || (e.GetProperty(name).ValueKind == JsonValueKind.Number && e.GetProperty(name).GetInt32() != 0);
}
