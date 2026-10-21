using TerrainEditor.App;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.Desktop;

/// <summary>
/// What a mod's tool asks of the editor (IToolContext): the area open, its card's values, its preview, the status bar,
/// and its edits made steps of the area's history (ToolCommit).
/// </summary>
/// <param name="view">The 3D view (its area, its preview).</param>
/// <param name="panel">The tool's card.</param>
/// <param name="status">Puts a line in the status bar.</param>
internal sealed class ModToolContext(GlView view, ModToolPanel panel, Action<Said> status) : IToolContext
{
    /// <summary>No area (none is open): no objects, flat ground at the sea.</summary>
    private static readonly PlainArea NoArea = new(0, 0, 0, 0, ValheimGen.WorldRules.Vanilla.WaterLevel);

    /// <inheritdoc/>
    public IAreaView Area => view.Scene is { } s ? new SceneLayers.AreaView(s) : NoArea;

    /// <inheritdoc/>
    public float Number(string id) => panel.Number(id);

    /// <inheritdoc/>
    public int Choice(string id) => panel.Choice(id);

    /// <inheritdoc/>
    public bool IsOn(string id) => panel.IsOn(id);

    /// <inheritdoc/>
    public void SetText(string id, string text) => panel.SetText(id, text ?? "");

    /// <inheritdoc/>
    public void Preview(IReadOnlyList<Shape> shapes)
    {
        if (view.Scene is { } s)
        {
            view.SetModPreview(s, shapes ?? []);
        }
    }

    /// <inheritdoc/>
    public void Status(string text) => status(Said.Raw(text ?? ""));

    /// <inheritdoc/>
    public void Commit(string label, ToolEdit edit)
    {
        if (view.Scene?.Session is not { } session || edit == null)
        {
            return;
        }
        var plan = ToolCommit.Apply(session, string.IsNullOrWhiteSpace(label) ? panel.Tool?.Info.Name ?? "" : label, edit);
        if (plan.Skipped.Count > 0)
        {
            status(Said.Of("mods.tool.skipped", plan.Skipped.Count));
        }
    }
}
