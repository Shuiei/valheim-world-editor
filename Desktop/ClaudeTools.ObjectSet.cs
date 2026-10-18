using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>A value of an object's data to set or take away (set_object_data).</summary>
    public sealed class FieldSpec
    {
        /// <summary>Which list it is in.</summary>
        [Description("Its kind: strings (text), ints (whole numbers), floats, longs (large whole numbers: player ids), vec3 (\"x y z\") or quats (\"x y z w\"). get_object gives each value's.")]
        public string Section { get; set; } = "strings";

        /// <summary>Its name (or hash in decimal).</summary>
        [Description("Its name, e.g. tag, text, health, fuel, creator, creatorName, owner, ownerName, permitted, enabled, level, tamed (or its hash, as get_object gives unknown ones).")]
        public string Key { get; set; } = "";

        /// <summary>The value as text; null takes it away.</summary>
        [Description("The value as text (numbers separated by spaces for vec3 and quats); null or left out: take the value away.")]
        public string? Value { get; set; }
    }

    /// <summary>The sections set_object_data takes (byte arrays only through set_contents).</summary>
    private static readonly HashSet<string> Sections = ["strings", "ints", "floats", "longs", "vec3", "quats"];

    /// <summary>set_object_data: values of an object's data set or taken away, as the inspector does.</summary>
    [McpServerTool(Name = "set_object_data", Title = "Change an object's values")]
    [Description("Sets or takes away values an object keeps in the save, as the editor's inspector does (get_object lists them): a portal's tag (strings tag; two portals with the same tag are linked), a sign's text (strings text), a builder (longs creator: a player id, 0 or none for a ruin; strings creatorName), a ward's owner and permitted players, health, fuel, a creature's level (ints level: stars + 1) or tamed (ints tamed 1), enabled… The object keeps everything else, gets a new id (the answer gives it), one pending step. Change values only when you know what they do: the game may reset or ignore wrong ones.")]
    public Task<string> SetObjectData(
        [Description("The object's id (list_objects).")] int id,
        [Description("The values to set or take away.")] FieldSpec[] values) => OnUi(() =>
    {
        if (EditStart() is string why)
        {
            return why;
        }
        var scene = _w.Session!.Scene;
        var (ok, problems) = Usable(scene, [id]);
        if (ok.Count == 0)
        {
            return problems[0];
        }
        if (values.Length == 0)
        {
            return "Nothing to change: give values.";
        }
        foreach (var v in values)
        {
            if (!Sections.Contains(v.Section))
            {
                return $"{v.Key}: the kind is strings, ints, floats, longs, vec3 or quats, not {v.Section} (contents: set_contents).";
            }
            if (string.IsNullOrWhiteSpace(v.Key))
            {
                return "Every value needs a key (its name).";
            }
            if (!Save.ZdoData.Valid(v.Section, v.Value))
            {
                return $"{v.Key}: \"{v.Value}\" is not a value of kind {v.Section}.";
            }
        }
        string name = _w.NameOfPrefab(scene.Things[id].Prefab) ?? "object";
        var (copy, problem) = Rewrite(id, [.. values.Select(v => new FieldChange(v.Section, v.Key, v.Value))], null, ClaudesStep(Lang.T("claude.undo.edited", name)));
        if (copy is not int newId)
        {
            return problem!;
        }
        _w.AfterClaudeEdit(Said.Of("claude.msg.edited", name));
        return $"Changed {name}; its id is now {newId}. Now pending: {Pending()}";
    });
}
