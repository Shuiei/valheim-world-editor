using ValheimWorldEditor.Sdk;

namespace WorldEditor.Tests;

/// <summary>
/// The editor as an SDK tool sees it (<see cref="IToolContext"/>), keeping what the tool did: the steps it committed, what
/// it previewed, its status line and the texts it set. The area and the controls' values are the test's; switches are off.
/// Shared by the tests of every tool (the editor's samples and the add-ons').
/// </summary>
/// <param name="area">The area the tool works on.</param>
public abstract class ToolContextRecorder(IAreaView area) : IToolContext
{
    /// <summary>The steps committed, in order.</summary>
    public List<(string Label, ToolEdit Edit)> Commits { get; } = [];

    /// <summary>The last preview.</summary>
    public IReadOnlyList<Shape> Shown { get; private set; } = [];

    /// <summary>The last status line.</summary>
    public string Said { get; private set; } = "";

    /// <summary>The texts set, by control.</summary>
    public Dictionary<string, string> Texts { get; } = [];

    /// <inheritdoc/>
    public IAreaView Area => area;

    /// <inheritdoc/>
    public abstract float Number(string id);

    /// <inheritdoc/>
    public virtual int Choice(string id) => 0;

    /// <inheritdoc/>
    public bool IsOn(string id) => false;

    /// <inheritdoc/>
    public void SetText(string id, string text) => Texts[id] = text;

    /// <inheritdoc/>
    public void Preview(IReadOnlyList<Shape> shapes) => Shown = shapes;

    /// <inheritdoc/>
    public void Status(string text) => Said = text;

    /// <inheritdoc/>
    public void Commit(string label, ToolEdit edit) => Commits.Add((label, edit));
}
