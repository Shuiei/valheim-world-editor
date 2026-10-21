using System.Text.RegularExpressions;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>The tools running extensions put on the tool rail (IEditorTool), each call guarded.</summary>
public static partial class ModExtensions
{
    /// <summary>
    /// A running extension's tool: its button and controls as read when it was registered, and its calls, each guarded
    /// (a call that throws turns the extension off, and the tool does nothing more).
    /// </summary>
    public sealed class Tool
    {
        private readonly Loaded _owner;
        private readonly IEditorTool _tool;

        private Tool(Loaded owner, IEditorTool tool, ToolInfo info, IReadOnlyList<ToolControl> controls)
        {
            _owner = owner;
            _tool = tool;
            Info = info;
            Controls = controls;
        }

        /// <summary>A tool of a running extension.</summary>
        internal static Tool Of(object owner, IEditorTool tool, ToolInfo info, IReadOnlyList<ToolControl> controls) => new((Loaded)owner, tool, info, controls);

        /// <summary>The mod offering it.</summary>
        public string Mod => _owner.Mod;

        /// <summary>Its name among the tools: the mod's name and the tool's id ("ExampleVehicles/canals"), unique.</summary>
        public string Name => Mod + "/" + Info.Id;

        /// <summary>Its button.</summary>
        public ToolInfo Info { get; }

        /// <summary>Its card's controls (those with an id, once each).</summary>
        public IReadOnlyList<ToolControl> Controls { get; }

        /// <summary>Whether its extension still runs (no call of it threw).</summary>
        public bool Running => _owner.Fault == null;

        /// <summary>Tells the tool it was chosen.</summary>
        public void Chosen(IToolContext ctx) => Guard(_owner, () => { _tool.OnChosen(ctx); return true; }, false);

        /// <summary>Tells the tool another was chosen.</summary>
        public void Left(IToolContext ctx) => Guard(_owner, () => { _tool.OnLeft(ctx); return true; }, false);

        /// <summary>Gives the tool the pointer.</summary>
        public void Pointed(IToolContext ctx, PointerInput input) => Guard(_owner, () => { _tool.OnPointer(ctx, input); return true; }, false);

        /// <summary>Gives the tool a key; true when it used it.</summary>
        public bool Pressed(IToolContext ctx, string key, KeyModifiers mods) => Guard(_owner, () => _tool.OnKey(ctx, key, mods), false);

        /// <summary>Tells the tool a control changed or was pressed.</summary>
        public void Control(IToolContext ctx, string id) => Guard(_owner, () => { _tool.OnControl(ctx, id); return true; }, false);
    }

    /// <summary>What a tool's id or a control's id may be: letters, digits, dots, dashes and underscores, 1 to 64.</summary>
    private static readonly Regex ToolIdForm = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The running extensions' tools, by mod then in the order registered: those whose button names an id of the
    /// right form, a name and help (the first of an id within a mod), with their controls of the right form (the first
    /// of an id). An extension whose tool throws when read is turned off.
    /// </summary>
    public static IReadOnlyList<Tool> Tools
    {
        get
        {
            var tools = new List<Tool>();
            foreach (var l in _state.Hooks)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var t in l.Tools)
                {
                    var read = Guard<(ToolInfo, IReadOnlyList<ToolControl>)?>(l, () => (t.Info, t.Controls ?? []), null);
                    if (read is var (info, controls) && ValidTool(info) && ids.Add(info.Id))
                    {
                        tools.Add(Tool.Of(l, t, info, ValidControls(controls)));
                    }
                }
            }
            return tools;
        }
    }

    private static bool ValidTool(ToolInfo? info) =>
        info != null && info.Id != null && ToolIdForm.IsMatch(info.Id) && !string.IsNullOrWhiteSpace(info.Name) && info.Help != null;

    private static List<ToolControl> ValidControls(IReadOnlyList<ToolControl> controls)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        return [.. controls.Where(c => c != null && c.Id != null && ToolIdForm.IsMatch(c.Id) && c.Label != null && Usable(c) && ids.Add(c.Id))];
    }

    /// <summary>Whether a control can be shown: a slider from a lower to a higher value, a choice of at least one option.</summary>
    private static bool Usable(ToolControl c) => c switch
    {
        SliderControl s => float.IsFinite(s.Min) && float.IsFinite(s.Max) && s.Max > s.Min && float.IsFinite(s.Value) && s.Step >= 0,
        ChoiceControl ch => ch.Options is { Count: > 0 } && ch.Options.All(o => o != null),
        _ => true,
    };
}
