using System.Numerics;
using ValheimWorldEditor.Sdk;
using static ExpandWorld.WorldEditor.ExpandWorldMod;

namespace ExpandWorld.WorldEditor;

/// <summary>
/// The Custom object tool: gives an object one of the Expand World mods' data entries (a "custom object", as the
/// mods' rules know it: a game object with the entry's values), by placing a new object with it where the player
/// clicks, or by setting it on the object clicked.
/// </summary>
/// <param name="host">The editor (its BepInEx folders).</param>
internal sealed class CustomObjectTool(IEditorHost host) : IEditorTool
{
    private const string DataId = "data", ActionId = "action", PrefabId = "prefab", InfoId = "info";

    /// <summary>The entries the card's lists were made from (read once: the card's lists cannot change).</summary>
    private CustomObjects? _listed;

    /// <summary>The entries as last read, when the tool was chosen.</summary>
    private CustomObjects _file = new([], "");

    /// <inheritdoc/>
    public ToolInfo Info { get; } = new("custom-object", "Custom object",
        "Gives an object one of the Expand World mods' data entries: placed new where you click, or set on the object you click.")
    {
        Icon = "M8 1l6 3.5v7L8 15l-6-3.5v-7L8 1zm0 1.7L3.5 5.3v5.4L8 13.3l4.5-2.6V5.3L8 2.7zM8 6a2 2 0 1 1 0 4a2 2 0 0 1 0-4z",
    };

    /// <summary>The objects the card lists for placing: those the rules use an entry on.</summary>
    private static List<string> Listed(CustomObjects file) => [.. file.Entries.SelectMany(e => e.Prefabs).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <inheritdoc/>
    public IReadOnlyList<ToolControl> Controls
    {
        get
        {
            _listed ??= _file = ReadCustomObjects(host.BepInExFolders);
            var prefabs = Listed(_listed);
            return
            [
                new ChoiceControl(DataId, "Data", _listed.Entries.Count > 0 ? [.. _listed.Entries.Select(e => e.Name)] : ["(none)"]),
                new ChoiceControl(ActionId, "A click", ["changes the object clicked", "places a new object"]),
                new ChoiceControl(PrefabId, "New object", prefabs.Count > 0 ? prefabs : ["(none named by the rules)"])
                {
                    Help = "The objects Expand World Prefabs' rules use a data entry on. For any other, place it with Place or Build first, then click it with this tool.",
                },
                new TextControl(InfoId, ""),
            ];
        }
    }

    /// <inheritdoc/>
    public void OnChosen(IToolContext ctx)
    {
        _file = ReadCustomObjects(host.BepInExFolders);
        Describe(ctx);
    }

    /// <inheritdoc/>
    public void OnLeft(IToolContext ctx)
    {
    }

    /// <inheritdoc/>
    public bool OnKey(IToolContext ctx, string key, KeyModifiers mods) => false;

    /// <inheritdoc/>
    public void OnControl(IToolContext ctx, string id) => Describe(ctx);

    /// <summary>Whether the files still list what the card does (else the card's lists name other things).</summary>
    private bool Same => _listed != null && _listed.Entries.Select(e => e.Name).SequenceEqual(_file.Entries.Select(e => e.Name)) && Listed(_listed).SequenceEqual(Listed(_file));

    private CustomData? Chosen(IToolContext ctx)
    {
        int i = ctx.Choice(DataId);
        return Same && i >= 0 && i < _file.Entries.Count ? _file.Entries[i] : null;
    }

    private string? Prefab(IToolContext ctx)
    {
        var prefabs = Listed(_file);
        int i = ctx.Choice(PrefabId);
        return Same && i >= 0 && i < prefabs.Count ? prefabs[i] : null;
    }

    private void Describe(IToolContext ctx)
    {
        if (_file.Problem != "")
        {
            ctx.SetText(InfoId, _file.Problem);
        }
        else if (Chosen(ctx) is not { } data)
        {
            ctx.SetText(InfoId, "The data files changed: restart the editor to list their entries.");
        }
        else
        {
            string used = data.Prefabs.Count > 0 ? $" The rules use it on: {string.Join(", ", data.Prefabs)}." : " No rule names an object for it.";
            string skipped = data.Skipped.Count > 0 ? $" Left out (set by the mod as it makes the object): {string.Join(", ", data.Skipped)}." : "";
            ctx.SetText(InfoId, $"{data.Name}: {data.Values.Count} value(s).{used}{skipped}");
        }
    }

    /// <inheritdoc/>
    public void OnPointer(IToolContext ctx, PointerInput input)
    {
        if (input.Action != PointerAction.Press)
        {
            return;
        }
        if (Chosen(ctx) is not { } data || data.Values.Count == 0)
        {
            Describe(ctx);
            ctx.Status("Custom object: nothing to set (see the tool's card).");
            return;
        }
        if (ctx.Choice(ActionId) == 0)
        {
            if (input.Target != PointerTarget.Thing || input.Thing is not { } thing)
            {
                ctx.Status("Custom object: click an object to give it the data.");
                return;
            }
            ctx.Commit($"Set {data.Name}", new ToolEdit().Change(thing, c => Apply(data, c)));
            ctx.Status($"{data.Name} set on {thing.Prefab}: {data.Values.Count} value(s).");
        }
        else if (input.Target is PointerTarget.None)
        {
            ctx.Status("Custom object: click the ground to place the object.");
        }
        else if (Prefab(ctx) is not { } prefab)
        {
            ctx.Status("Custom object: no object to place. Place one with Place or Build, then click it with this tool.");
        }
        else
        {
            ctx.Commit($"Place {prefab} ({data.Name})", new ToolEdit().Add(prefab, input.Point, Vector3.Zero, c => Apply(data, c)));
            ctx.Status($"{prefab} placed with {data.Name}: {data.Values.Count} value(s).");
        }
    }

    /// <summary>Sets an entry's values on an object.</summary>
    internal static void Apply(CustomData data, ICopy copy)
    {
        foreach (var value in data.Values)
        {
            value.Set(copy);
        }
    }
}
