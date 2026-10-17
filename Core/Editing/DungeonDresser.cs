using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

/// <summary>
/// What makes a generated dungeon's rooms what they are: their furniture and set pieces (colonnades,
/// thrones on a dais, pews before an altar, sarcophagi, cells behind bars...), their lights, what hangs
/// on their walls and lies on their floors, their decay, who guards them and what they hold. Each room
/// is dressed in its own frame: u across (right of the way in), v deep (0 its middle, the way in behind).
/// </summary>
internal sealed partial class DungeonDresser
{
    /// <summary>
    /// What stands on a floor goes 5 cm up: a piece whose middle of mass is right on a floor's surface
    /// gets no support from it in game.
    /// </summary>
    internal const float Lift = 0.05f;

    private readonly Material _mat;
    /// <summary>The biome's palette (lights, foes, loot, clutter).</summary>
    private readonly Biome _b;
    private readonly Style _style;
    private readonly DungeonGen.Settings _s;
    private readonly Random _rnd;
    /// <summary>Where the dungeon's objects go.</summary>
    private readonly DungeonOut _o;
    /// <summary>How ruined (0 kept to 1): the settings' Decay, else the style's.</summary>
    private readonly float _decay;
    /// <summary>Where the corridor being dressed has its pilasters' bands of wall (v), for a gate to keep off them.</summary>
    private readonly List<float> _bands = [];
    /// <summary>The foes to use: those picked in the settings, else the biome's.</summary>
    private readonly Foe[] _foes;
    /// <summary>The elite guards: the chosen foes when the settings name some, else the biome's elites.</summary>
    private readonly Foe[] _elites;
    /// <summary>The arena's boss: the one picked, the biome's when none is, null when "" is.</summary>
    private readonly Foe? _boss;
    /// <summary>The dungeon's name, for the entrance's sign.</summary>
    private readonly string _name;
    /// <summary>The lights the room being furnished asked for: 0 none, 1 the usual, 2 whatever the settings (important).</summary>
    private int _lights;
    /// <summary>Whether the room being furnished asked for its decay.</summary>
    private bool _decayed;

    /// <summary>Sets up a dresser for one dungeon.</summary>
    private DungeonDresser(Material mat, Biome b, Style style, DungeonGen.Settings s, Random rnd, DungeonOut o, string name)
    {
        _mat = mat;
        _b = b;
        _style = style;
        _s = s;
        _rnd = rnd;
        _o = o;
        _name = name;
        _decay = Math.Clamp(s.Decay ?? style.Decay, 0, 1);
        _foes = s.Foes is { Count: > 0 } chosen ? [.. chosen.Select(FoeOf)] : b.Foes;
        _elites = s.Foes is { Count: > 0 } ? _foes : b.Elites;
        _boss = s.Boss == null ? b.Boss : s.Boss.Length == 0 ? null : FoeOf(s.Boss);
    }

    /// <summary>Furnishes, lights, guards and stocks every space of the plan.</summary>
    public static void Dress(DungeonPlan plan, Material mat, Biome b, Style style, DungeonGen.Settings s, Random rnd, DungeonOut o, string name)
    {
        var d = new DungeonDresser(mat, b, style, s, rnd, o, name);
        foreach (var sp in plan.Spaces)
        {
            d.Furnish(new Frame(sp, mat, plan.Levels[sp.Level]));
        }
    }

    /// <summary>
    /// A foe by its spawner's or its creature's name; one not in any biome is taken as it is (a Spawner_
    /// name for both, its creature the name without the prefix).
    /// </summary>
    public static Foe FoeOf(string name)
    {
        foreach (var f in Biomes.SelectMany(b => b.Foes.Concat(b.Elites).Append(b.Boss)))
        {
            if (f.Spawner == name || f.Creature == name)
            {
                return f;
            }
        }
        return name.StartsWith("Spawner_", StringComparison.Ordinal) ? new Foe(name, name["Spawner_".Length..]) : new Foe(name, name);
    }
}
