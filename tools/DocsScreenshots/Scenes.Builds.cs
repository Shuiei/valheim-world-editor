
namespace TerrainEditor.DocsScreenshots;

/// <summary>The scenes' builds and placed content: the Place tool's kinds and pieces, nature, levelling, trees and huts.</summary>
public sealed partial class Scenes
{
    /// <summary>The Place tool's kinds: exactly these.</summary>
    private static void Kinds(Editor e, params string[] names)
    {
        e.Send("click Choose kinds…");
        e.Send("click Untick all");
        foreach (string n in names)
        {
            e.Send($"type Search kinds|{n}");
            e.Send($"click {n}");
        }
        e.Send("type Search kinds|");
        e.Send("click Done");
    }

    /// <summary>
    /// Objects as the game places its own (a spawn point, a boulder, crops in a field): true size, upright, all facing
    /// the rotation (set in Brush mode, the only one with Tilt; the mode is chosen again afterwards).
    /// </summary>
    private static void Pieces(Editor e)
    {
        e.Send("click Brush");
        e.Send("set Tilt|0");
        e.Send("set Size %|100,100");
        e.Send("set Rotation|0");
        e.Send("check Random facing (off: all face the rotation)|off");
    }

    /// <summary>The Build tool on, with this piece picked in its dock (by its prefab name), laid as it was last (see <see cref="Lay"/>).</summary>
    private static void Piece(Editor e, string prefab)
    {
        e.Send("key U");
        e.Send($"type Search pieces|{prefab}");
        e.Send($"click {prefab}");
        e.Send("type Search pieces|");
    }

    /// <summary>The Build dock's Lay: One piece, Line, Circle, Rectangle or Grid.</summary>
    private static void Lay(Editor e, string how) => e.Send($"click {how}");

    /// <summary>One piece put down with Build where the hammer would put it at that point (on the ground, or on the piece there).</summary>
    private static void BuildOne(Editor e, string prefab, (double X, double Z) at)
    {
        Piece(e, prefab);
        Lay(e, "One piece");
        Move(e, at);
        Click(e, at);
    }

    /// <summary>A floor laid with Build: the piece in a grid over the box, so far apart, lifted so many half metres (PgUp).</summary>
    private static void BuildFloor(Editor e, string prefab, (double X, double Z) a, (double X, double Z) b, double spacing = 2, int halfMetresUp = 0)
    {
        Piece(e, prefab);
        Lay(e, "Grid");
        e.Send($"set Spacing|{spacing.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        for (int i = 0; i < halfMetresUp; i++)
        {
            e.Send("key PageUp");
        }
        Drag(e, a, b);
        e.Send("key Return");
        if (halfMetresUp > 0)
        {
            e.Send("click Reset");
        }
        e.Send("click Clear shape (Esc)");
        Lay(e, "One piece");
    }

    /// <summary>Walls laid with Build end to end round a rectangle (or a circle: from its centre to its edge), this many high.</summary>
    private static void BuildWalls(Editor e, string prefab, (double X, double Z) a, (double X, double Z) b, int layers = 1, string shape = "Rectangle")
    {
        Piece(e, prefab);
        Lay(e, shape);
        e.Send($"set Layers|{layers}");
        Drag(e, a, b);
        e.Send("key Return");
        e.Send("click Clear shape (Esc)");
        Lay(e, "One piece");
    }

    /// <summary>Trees and rocks as nature has them: sizes, tilts and facings vary.</summary>
    private static void Nature(Editor e)
    {
        e.Send("set Tilt|4");
        e.Send("set Size %|80,120");
    }

    /// <summary>One object of the chosen kinds exactly where clicked (Place, Brush mode).</summary>
    private static void Single(Editor e, (double X, double Z) at)
    {
        e.Send("click Brush");
        e.Send("check One at a time, exactly at the cursor|on");
        Click(e, at);
    }

    /// <summary>Ground levelled at its average height, its edge softened (a change of this scene, not saved).</summary>
    private static void Level(Editor e)
    {
        Level(e, P(-16, -14), P(16, 14));
    }

    /// <summary>The ground of the box from a to b levelled at its average height, its edge softened.</summary>
    private static void Level(Editor e, (double X, double Z) a, (double X, double Z) b, double soft = 8)
    {
        AreaAction(e, a, b, "Flatten", apply: false);
        e.Send($"set Soft edge|{soft}");
        e.Send("click avg");
        e.Send("key Return");
        e.Send("click Clear selection (Esc)");
    }

    /// <summary>The trees <c>Trees</c> plants unless told otherwise.</summary>
    private static readonly string[] Woods = ["Beech1", "Oak1", "Birch1"];

    /// <summary>A wood planted with the Place brush along a stroke.</summary>
    private static void Trees(Editor e, (double X, double Z) a, (double X, double Z) b, int density = 6, string[]? kinds = null)
    {
        e.Send("key T");
        Kinds(e, kinds ?? Woods);
        e.Send("click Brush");
        e.Send("check One at a time, exactly at the cursor|off");
        Nature(e);
        e.Send("set Brush size|8");
        e.Send($"set Density|{density}");
        Drag(e, a, b, steps: 6, hold: 120);
        e.Send("key Escape");
    }

    /// <summary>A wooden hut: a floor of 2 m pieces with walls end to end around it, this many high (the Build tool).</summary>
    private static void Hut(Editor e, (double X, double Z) a, (double X, double Z) b, int layers = 2)
    {
        BuildFloor(e, "wood_floor", a, b);
        BuildWalls(e, "woodwall", a, b, layers);
    }

    /// <summary>A small wooden house on a floor, a crop field and a fire pit, inside a palisade.</summary>
    private static void BuildBase(Editor e)
    {
        Level(e, P(-18, -18), P(18, 14));
        // The field: cultivated ground.
        e.Send("key D7");
        e.Send("set Size|3");
        e.Send("set Strength|1");
        foreach (int dz in new[] { -14, -11, -8 })
        {
            Path(e, [P(-2, dz), P(14, dz)], steps: 6, hold: 150);
        }
        Hut(e, P(-14, 2), P(-2, 10));
        e.Send("key T");
        // Carrots and turnips in the field.
        Kinds(e, "sapling_carrot", "sapling_turnip");
        e.Send("click Grid");
        e.Send("set Spacing|1");
        Drag(e, P(-1, -15), P(13, -7));
        e.Send("key Return");
        // A fire pit and a chest by the house.
        BuildOne(e, "fire_pit", P(4, 4));
        BuildOne(e, "piece_chest_wood", P(-1, 8));
        // A palisade around it all.
        BuildWalls(e, "stake_wall", P(0, 0), P(24, 0), shape: "Circle");
        e.Send("key Escape");
    }
}
