using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's changes to what an object holds (set_contents): a container's items, a stand's item or armour, a sign's text, a
/// creature's stars.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>What a container (or a stand) gets: one stack.</summary>
    public sealed class ItemSpec
    {
        /// <summary>The item's prefab name.</summary>
        [Description("The item's prefab name, e.g. Coins, Amber, Ruby, IronScrap, SwordIron (find_prefabs with items: true).")]
        public string Item { get; set; } = "";

        /// <summary>How many in the stack.</summary>
        [Description("How many in the stack (default 1).")]
        public int Stack { get; set; } = 1;

        /// <summary>Quality (upgrade level).</summary>
        [Description("Quality (upgrade level) for weapons and armour (default 1).")]
        public int Quality { get; set; } = 1;

        /// <summary>Variant: a shield's or banner's style, from 0.</summary>
        [Description("Variant: a shield's or banner's style, from 0 (default 0).")]
        public int Variant { get; set; }
    }

    /// <summary>
    /// set_contents: a container's items, a stand's item or armour (with orientation or pose), a sign's text or a
    /// creature's stars, as the inspector changes them; the object is replaced by a changed copy (a new id).
    /// </summary>
    [McpServerTool(Name = "set_contents", Title = "Fill a chest or a stand, write a sign, set stars")]
    [Description("Changes what an object holds, as the editor's inspector does: a container's items (they replace what it held, laid out in its slots in order); an item stand's item (itemstand on a wall, itemstandh lying flat: a table's food, a trophy, a weapon; one item, none to empty it) and the way it hangs; an armour stand's armour, cape, belt, shield and weapon (each in the slot that takes it; the others emptied) and its pose; a sign's text; a creature's stars (0 to 2). Stands take what the game lets them (find_prefabs with items: true for names). One pending step; the object keeps everything else it holds.")]
    public Task<string> SetContents(
        [Description("The object's id (list_objects, place_pieces).")] int id,
        [Description("For a container (chest, barrel…): its items; for an item stand: one item (or none: empty); for an armour stand: what it wears (optional).")] ItemSpec[]? items = null,
        [Description("For a sign: its text (optional).")] string? text = null,
        [Description("For a creature: its stars, 0 to 2 (optional).")] int? stars = null,
        [Description("For an item stand: the way the item hangs, from 0 (the game's orientations, as alt + use cycles them; optional).")] int? orientation = null,
        [Description("For an armour stand: its pose, from 0 (optional).")] int? pose = null) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var s = _w.Session!;
        if (ObjectProblem(s.Scene, id) is string refused)
        {
            return refused;
        }
        var t = s.Scene.Things[id];
        string kindName = _w.NameOfPrefab(t.Prefab) ?? "";
        var set = new List<FieldChange>();
        List<ItemUpload>? inv = null;
        try
        {
            // What a stand takes (an item stand's item, an armour stand's armour, or its orientation or pose alone); the items left go to a container.
            var (left, refusal) = StandChanges(t.Prefab, kindName, items, orientation, pose, set);
            if (refusal != null)
            {
                return refusal;
            }
            items = left;
        }
        catch (ArgumentException ex)
        {
            Log.CaughtBrief(ex, "reading the values Claude gave for an object");
            return ex.Message;
        }
        if (items != null)
        {
            var (uploads, containerRefusal) = ContainerItems(t.Prefab, kindName, items);
            if (containerRefusal != null)
            {
                return containerRefusal;
            }
            inv = uploads;
        }
        if (TextAndStars(t.Prefab, kindName, text, stars, set) is string textRefusal)
        {
            return textRefusal;
        }
        if (set.Count == 0 && inv == null)
        {
            return "Nothing to change: give items, text, stars, an orientation or a pose.";
        }
        string name = _w.NameOfPrefab(t.Prefab) ?? "object";
        var notes = new List<string>();
        var (copy, problem) = Rewrite(id, set, inv, ClaudesStep(Lang.T("claude.undo.filled", name)), notes);
        if (copy is not int newId)
        {
            return problem!;
        }
        _w.AfterClaudeEdit(Said.Of("claude.msg.filled", name));
        return $"Changed {name}; its id is now {newId}." + string.Concat(notes.Select(n => " " + n + "."));
    });
}
