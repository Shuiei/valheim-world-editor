
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
    /// Building pieces as the game places them: true size, upright, all facing the rotation (set in Brush mode, the only
    /// one with Tilt; the mode is chosen again afterwards).
    /// </summary>
    private static void Pieces(Editor e)
    {
        e.Send("click Brush");
        e.Send("set Tilt|0");
        e.Send("set Size %|100,100");
        e.Send("set Rotation|0");
        e.Send("check Random facing (off: all face the rotation)|off");
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
        e.Send("click Clear (Esc)");
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

    /// <summary>A wooden hut: a floor of 2 m pieces with walls end to end around it, this many high.</summary>
    private static void Hut(Editor e, (double X, double Z) a, (double X, double Z) b, int layers = 2)
    {
        e.Send("key T");
        Pieces(e);
        Kinds(e, "wood_floor");
        e.Send("click Grid");
        e.Send("set Spacing|2");
        Drag(e, a, b);
        e.Send("key Return");
        Kinds(e, "woodwall");
        e.Send("click Line");
        e.Send("click Rectangle");
        e.Send($"set Layers|{layers}");
        Drag(e, a, b);
        e.Send("key Return");
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
        Kinds(e, "fire_pit");
        Single(e, P(4, 4));
        Kinds(e, "piece_chest_wood");
        Single(e, P(-1, 8));
        e.Send("check One at a time, exactly at the cursor|off");
        // A palisade around it all.
        Kinds(e, "stake_wall");
        e.Send("click Line");
        e.Send("click Circle");
        Drag(e, P(0, 0), P(24, 0));
        e.Send("key Return");
        e.Send("key Escape");
    }
}
