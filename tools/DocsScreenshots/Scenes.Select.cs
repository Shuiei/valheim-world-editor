using System.Globalization;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The Select tool's and the Inspector's pictures that need a filled chest or input on the display (select.md and its
/// guides), and the display input they use: what the driver cannot name (the Inspector's slots, its item search) is
/// clicked and typed on the display with xdotool.
/// </summary>
public sealed partial class Scenes
{
    /// <summary>The Inspector's first slot's middle, in the window's pixels (its card on the right, a chest's grid).</summary>
    private const int SlotLeft = 1062, SlotTop = 168;

    /// <summary>The distance between two slots' middles, in pixels.</summary>
    private const int SlotStep = 46;

    /// <summary>The box drawn on the screen over the hut's west half (east is left at the usual turn): its corners.</summary>
    private static readonly (double X, double Z) HutBoxFrom = P(0, -7), HutBoxTo = P(-8, 5.5);

    /// <summary>select-arrows.jpg: close on a selected rock and its arrows and ring, the Select panel beside it.</summary>
    private void SelectArrows(Editor e)
    {
        Bare(e);
        Level(e);
        e.Send("key T");
        Nature(e);
        Kinds(e, "Rock_7");
        // Off the zone's middle: an object stands there, and One at a time keeps 0.3 m from any.
        Single(e, P(3, 2));
        e.Send("key E");
        Click(e, P(3, 2));
        Camera(e, 3, 2, 200, 30, 5);
        e.Send("wait 300");
        e.Shot(Out("select-arrows.jpg"));
    }

    /// <summary>
    /// select-ctrl-click.jpg: three of a hut's wall pieces selected, one click and two Ctrl + clicks, seen from eye
    /// height: the last one clicked lighter.
    /// </summary>
    private void SelectCtrlClick(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e);
        Hut(e, P(-4, -3), P(4, 3));
        e.Send("key E");
        Camera(e, 3, 0, 200, 24, 16);
        e.Send("wait 300");
        // Points inside the hut: the ray to each crosses the near (north) wall's lower row.
        Click(e, P(3, 1.5));
        Click(e, P(-1, 1.5), "ctrl");
        Click(e, P(-3, 1.5), "ctrl");
        e.Send("wait 300");
        e.Shot(Out("select-ctrl-click.jpg"));
    }

    /// <summary>
    /// select-box-drawing.jpg and select-box.jpg: a box drawn on the screen from the hut's middle over its west half,
    /// then what it took: the pieces seen inside it.
    /// </summary>
    private void SelectBox(Editor e)
    {
        HutInView(e);
        Drag(e, HutBoxFrom, HutBoxTo, mods: "alt", release: false);
        e.Shot(Out("select-box-drawing.jpg"));
        e.Send($"mouse up {HutBoxTo.X} {HutBoxTo.Z} left alt");
        e.Send("wait 300");
        e.Shot(Out("select-box.jpg"), keepMessage: true);
    }

    /// <summary>select-through.jpg: the same box with Select through on: what is behind the walls is taken too.</summary>
    private void SelectThrough(Editor e)
    {
        HutInView(e);
        e.Send("check Select through (Alt + Z)|on");
        Drag(e, HutBoxFrom, HutBoxTo, mods: "alt");
        e.Send("wait 300");
        e.Shot(Out("select-through.jpg"), keepMessage: true);
    }

    /// <summary>select-hide.jpg: close on the hut once its boxed west half is hidden (Ctrl + H): the chest inside shows.</summary>
    private void SelectHide(Editor e)
    {
        HutInView(e);
        Drag(e, HutBoxFrom, HutBoxTo, mods: "alt");
        e.Send("key H ctrl");
        Camera(e, 3, 0, 200, 40, 17);
        e.Send("wait 300");
        e.Shot(Out("select-hide.jpg"), keepMessage: true);
    }

    /// <summary>
    /// select-moving.jpg: a hut selected whole, its red X arrow dragged 4.5 m with the button still down: the status
    /// bar follows the move.
    /// </summary>
    private void SelectMoving(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e);
        Hut(e, P(-4, -3), P(4, 3));
        e.Send("key E");
        Camera(e, 4, 0, 200, 40, 26);
        e.Send("wait 300");
        Click(e, P(4, 0));
        e.Send("click Whole building");
        e.Send("wait 300");
        // The red arrow lies along x from the selection's middle, at its foot.
        Drag(e, P(1.5, 0), P(6, 0), steps: 6, mods: "", release: false);
        e.Send("wait 300");
        e.Shot(Out("select-moving.jpg"), keepMessage: true);
        e.Send($"mouse up {P(6, 0).X} {P(6, 0).Z}");
    }

    /// <summary>
    /// guide-chest-slot.jpg: a wood chest open in the Inspector, three slots filled and a fourth being filled (IronScrap,
    /// How many 20), before Apply changes; inspector.jpg: close on the chest once applied, open again in the Inspector:
    /// its slots with the items, then its data.
    /// </summary>
    private void Inspector(Editor e)
    {
        Bare(e);
        Level(e);
        e.Send("key T");
        Pieces(e);
        Kinds(e, "piece_chest_wood");
        Single(e, P(3, 2));
        e.Send("key E");
        Click(e, P(3, 2));
        e.Send("key I");
        ChestItem(e, 0, 0, "Wood", 20);
        ChestItem(e, 1, 0, "Coins", 50);
        ChestItem(e, 2, 0, "Resin", 12);
        ChestItem(e, 3, 0, "ronscr", 20, choose: true);
        Camera(e, 1, 2, 200, 35, 7);
        e.Send("wait 300");
        e.Shot(Out("guide-chest-slot.jpg"));
        e.Send("click Apply changes");
        e.Send("wait 300");
        e.Shot(Out("inspector.jpg"));
    }

    /// <summary>
    /// One item put in a slot of the wood chest open in the Inspector, as a player does it: the slot clicked, the
    /// item's name typed in Find an item… (the box under the chest's two rows of slots), or a few letters of it and the
    /// first item its list offers chosen, How many set.
    /// </summary>
    /// <param name="e">The editor.</param>
    /// <param name="column">The slot's column, from 0.</param>
    /// <param name="row">The slot's row, from 0.</param>
    /// <param name="item">
    /// The item's whole name, none of its beginnings another item's name: the box takes a whole name as soon as it is
    /// typed and is drawn again, so keys typed after it (Scrap after Iron) would go to the window's shortcuts.
    /// </param>
    /// <param name="count">How many.</param>
    /// <param name="choose">
    /// The item is a few letters of the name (no whole name): the first item the list offers is chosen.
    /// </param>
    private static void ChestItem(Editor e, int column, int row, string item, int count, bool choose = false)
    {
        ClickPixel(e, SlotLeft + column * SlotStep, SlotTop + row * SlotStep);
        // Keys go to the window under the pointer (no window manager): it stays there until the item is taken.
        ClickPixel(e, SlotLeft + 140, SlotTop + 104, leave: false);
        TypeKeys(e, item);
        if (choose)
        {
            RunOnDisplay("xdotool", "key", "Down", "Return");
        }
        RunOnDisplay("xdotool", "mousemove", "1915", "1075");
        e.Send("wait 300");
        e.Send($"set How many|{count}");
    }

    /// <summary>A left click at a pixel of the editor's window, on the display.</summary>
    /// <param name="e">The editor.</param>
    /// <param name="x">The pixel's column in the window.</param>
    /// <param name="y">The pixel's row in the window.</param>
    /// <param name="leave">Move the pointer off the window afterwards (where it shows no tip).</param>
    private static void ClickPixel(Editor e, int x, int y, bool leave = true)
    {
        string window = RunOnDisplay("xdotool", "search", "--onlyvisible", "--name", "Valheim World Editor").Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1];
        RunOnDisplay("xdotool", "mousemove", "--window", window, x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture));
        e.Send("wait 200");
        RunOnDisplay("xdotool", "click", "1");
        e.Send("wait 200");
        if (leave)
        {
            RunOnDisplay("xdotool", "mousemove", "1915", "1075");
        }
        e.Send("wait 300");
    }

    /// <summary>Keys typed on the display, into the window under the pointer.</summary>
    private static void TypeKeys(Editor e, string text)
    {
        RunOnDisplay("xdotool", "type", "--delay", "40", text);
        e.Send("wait 300");
    }
}
