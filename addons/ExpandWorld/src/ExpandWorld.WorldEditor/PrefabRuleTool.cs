using System.Numerics;
using ValheimWorldEditor.Sdk;
using static ExpandWorld.WorldEditor.ExpandWorldMod;

namespace ExpandWorld.WorldEditor;

/// <summary>
/// The Prefab rules tool: shows Expand World Prefabs' rules (expand_prefabs*.yaml) and marks the objects of the open
/// area a rule would act on, green when every condition holds, amber when some only the game knows. It changes
/// nothing: the rules run in the game.
/// </summary>
/// <param name="host">The editor (its BepInEx folders).</param>
internal sealed class PrefabRuleTool(IEditorHost host) : IEditorTool
{
    private const string RuleId = "rule", InfoId = "info";

    private static readonly Vector4 SureMark = new(0.35f, 0.95f, 0.45f, 0.9f), MaybeMark = new(1f, 0.75f, 0.2f, 0.9f);

    /// <summary>The rules the card's list was made from (read once: the card's list cannot change).</summary>
    private PrefabRules? _listed;

    /// <summary>The rules as last read, when the tool was chosen.</summary>
    private PrefabRules _file = new([], "");

    /// <inheritdoc/>
    public ToolInfo Info { get; } = new("prefab-rules", "Prefab rules",
        "Shows Expand World Prefabs' rules and marks the objects a rule would act on. Changes nothing.")
    {
        Icon = "M2 3h12v2H2zM2 7h8v2H2zM2 11h5v2H2zM12 9l3 3-3 3-1-1 2-2-2-2z",
    };

    /// <inheritdoc/>
    public IReadOnlyList<ToolControl> Controls
    {
        get
        {
            _listed ??= _file = ReadPrefabRules(host.BepInExFolders);
            return
            [
                new ChoiceControl(RuleId, "Rule", ["All rules on object creation", .. _listed.Rules.Select(r => r.Title)]),
                new TextControl(InfoId, ""),
            ];
        }
    }

    /// <inheritdoc/>
    public void OnChosen(IToolContext ctx)
    {
        _file = ReadPrefabRules(host.BepInExFolders);
        Show(ctx);
    }

    /// <inheritdoc/>
    public void OnLeft(IToolContext ctx) => ctx.Preview([]);

    /// <inheritdoc/>
    public bool OnKey(IToolContext ctx, string key, KeyModifiers mods) => false;

    /// <inheritdoc/>
    public void OnControl(IToolContext ctx, string id) => Show(ctx);

    /// <summary>Whether the files still hold the rules the card lists.</summary>
    private bool Same => _listed != null && _listed.Rules.Select(r => r.Title).SequenceEqual(_file.Rules.Select(r => r.Title));

    /// <summary>The rules chosen: one, or all those that run when an object is made (null: the list is out of date).</summary>
    private IReadOnlyList<PrefabRule>? Chosen(IToolContext ctx)
    {
        int i = ctx.Choice(RuleId);
        if (!Same)
        {
            return null;
        }
        return i <= 0 ? [.. _file.Rules.Where(r => r.OnCreate)] : i - 1 < _file.Rules.Count ? [_file.Rules[i - 1]] : null;
    }

    private void Show(IToolContext ctx)
    {
        if (_file.Problem != "")
        {
            ctx.Preview([]);
            ctx.SetText(InfoId, _file.Problem);
            return;
        }
        if (Chosen(ctx) is not { } rules)
        {
            ctx.Preview([]);
            ctx.SetText(InfoId, "The rule files changed: restart the editor to list their rules.");
            return;
        }
        var marked = Marked(ctx.Area, rules, _file.Rules);
        ctx.Preview([.. marked.Select(m => Mark(m.Object.Position, m.Match == RuleMatch.Yes ? SureMark : MaybeMark))]);
        int sure = marked.Count(m => m.Match == RuleMatch.Yes), maybe = marked.Count - sure;
        string found = $" In this area: {sure} object(s) it acts on (green), {maybe} it may act on (amber).";
        ctx.SetText(InfoId, ctx.Choice(RuleId) <= 0
            ? $"{rules.Count} of {_file.Rules.Count} rule(s) run when an object is made.{found} Click an object to see the rules on it."
            : Describe(rules[0]) + found);
    }

    /// <summary>The objects of the area the rules act on, each with how sure the editor is (the surest rule's).</summary>
    /// <param name="area">The area.</param>
    /// <param name="rules">The rules shown.</param>
    /// <param name="all">Every rule of the files (those the shown ones are drawn among).</param>
    internal static List<(IObjectView Object, RuleMatch Match)> Marked(IAreaView area, IReadOnlyList<PrefabRule> rules, IReadOnlyList<PrefabRule> all)
    {
        Dictionary<IObjectView, RuleMatch> best = new(ReferenceEqualityComparer.Instance);
        foreach (string prefab in rules.SelectMany(r => r.Prefabs).Distinct(StringComparer.Ordinal))
        {
            foreach (var obj in area.ObjectsOf(prefab))
            {
                int biome = area.Biome(obj.Position.X, obj.Position.Z);
                var match = rules.Select(r => MatchAmong(r, all, obj, biome)).Max();
                if (match != RuleMatch.No)
                {
                    best[obj] = match;
                }
            }
        }
        return [.. best.Select(e => (e.Key, e.Value))];
    }

    /// <summary>A rule in words: what starts it, what it is for, what it does, and what the editor cannot check.</summary>
    internal static string Describe(PrefabRule rule)
    {
        List<string> parts = [$"#{rule.Number}, on {string.Join(" or ", rule.Triggers)}{(rule.OnCreate ? "" : " (in the game only)")}."];
        parts.Add(rule.Prefabs.Count > 0 ? $"For: {string.Join(", ", rule.Prefabs)}." : "For no object the editor knows by name.");
        if (rule.Unlisted.Count > 0)
        {
            parts.Add($"Not marked (wildcards, groups or unknown names): {string.Join(", ", rule.Unlisted)}.");
        }
        if (rule.Excluded.Count > 0)
        {
            parts.Add($"Except: {string.Join(", ", rule.Excluded)}.");
        }
        parts.Add(rule.Actions.Count > 0 ? $"It {string.Join(", ", rule.Actions)}." : "It does nothing the editor can name.");
        if (rule.GameOnly.Count > 0)
        {
            parts.Add($"Only the game checks: {string.Join(", ", rule.GameOnly)}.");
        }
        return string.Join(" ", parts);
    }

    /// <inheritdoc/>
    public void OnPointer(IToolContext ctx, PointerInput input)
    {
        if (input.Action != PointerAction.Press)
        {
            return;
        }
        if (input.Target != PointerTarget.Thing || input.Thing is not { } thing)
        {
            ctx.Status("Prefab rules: click an object to see the rules on it.");
            return;
        }
        int biome = ctx.Area.Biome(thing.Position.X, thing.Position.Z);
        var on = _file.Rules.Select(r => (Rule: r, Match: MatchAmong(r, _file.Rules, thing, biome))).Where(m => m.Match != RuleMatch.No).ToList();
        ctx.Status(on.Count == 0
            ? $"Prefab rules: no rule acts on this {thing.Prefab}."
            : $"Prefab rules on this {thing.Prefab}: " + string.Join("; ", on.Select(m =>
                $"#{m.Rule.Number} {(m.Match == RuleMatch.Yes ? "acts" : "may act")} on {string.Join(" or ", m.Rule.Triggers.Select(t => t.Split(',')[0].Trim()))}" +
                (m.Rule.Actions.Count > 0 ? $" ({string.Join(", ", m.Rule.Actions)})" : ""))) + ".");
    }

    /// <summary>A mark over an object: a thin upright cross from 1 m to 4 m above it.</summary>
    internal static Shape Mark(Vector3 at, Vector4 color)
    {
        const float Half = 0.25f, Low = 1f, High = 4f;
        Vector3[] v =
        [
            at + new Vector3(-Half, Low, 0), at + new Vector3(Half, Low, 0), at + new Vector3(Half, High, 0), at + new Vector3(-Half, High, 0),
            at + new Vector3(0, Low, -Half), at + new Vector3(0, Low, Half), at + new Vector3(0, High, Half), at + new Vector3(0, High, -Half),
        ];
        return new Shape(v, [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7], color) { WorldSpace = true, BothSides = true };
    }
}
