using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// The game's terrain operations' settings (the hoe's and the cultivator's strokes) by the hash of their object's
/// name, as the game's ObjectDB.TryGetTerrainOp finds them: a <see cref="DigKind.Hoe"/> edit names its stroke so, and
/// is replayed with the settings of the game it is loaded in. Read from the player's game through the editor (the
/// manifest's "gameComponents": TerrainOp).
/// </summary>
public static class TerrainOps
{
    /// <summary>The game script whose components hold the settings.</summary>
    public const string ScriptClass = "TerrainOp";

    /// <summary>The settings by object name hash, from the game's TerrainOp components (the first of a name).</summary>
    /// <param name="components">The components, as the editor read them.</param>
    public static Dictionary<int, HoeSettings> From(IEnumerable<GameComponent> components)
    {
        var byHash = new Dictionary<int, HoeSettings>();
        foreach (var c in components)
        {
            int hash = Hash.Of(c.ObjectName);
            if (!byHash.ContainsKey(hash) && c.Fields.TryGetValue("m_settings", out object? value) && value is IReadOnlyDictionary<string, object?> s)
            {
                byHash[hash] = Read(s);
            }
        }
        return byHash;
    }

    /// <summary>One operation's settings, fields named as the game's; a field missing reads as 0 (off).</summary>
    /// <param name="s">The settings' fields.</param>
    public static HoeSettings Read(IReadOnlyDictionary<string, object?> s) => new(
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
        PaintType: (int)N(s, "m_paintType"),
        PaintRadius: F(s, "m_paintRadius"));

    private static double N(IReadOnlyDictionary<string, object?> s, string name) => s.TryGetValue(name, out object? v) ? v switch
    {
        long l => l,
        double d => d,
        bool b => b ? 1 : 0,
        _ => 0,
    } : 0;

    private static float F(IReadOnlyDictionary<string, object?> s, string name) => (float)N(s, name);

    private static bool B(IReadOnlyDictionary<string, object?> s, string name) => Math.Abs(N(s, name)) > 0;
}
