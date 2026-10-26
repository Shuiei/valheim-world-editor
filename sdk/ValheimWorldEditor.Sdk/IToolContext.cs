using System.Collections.Generic;
using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>What a tool can ask of the editor while it is chosen (<see cref="IEditorTool"/>).</summary>
public interface IToolContext
{
    /// <summary>The area open, as it is now (its objects with the changes not saved yet, its ground).</summary>
    IAreaView Area { get; }

    /// <summary>A slider's value.</summary>
    /// <param name="id">The slider's id.</param>
    float Number(string id);

    /// <summary>A choice's index (0 for the first).</summary>
    /// <param name="id">The choice's id.</param>
    int Choice(string id);

    /// <summary>A toggle's value.</summary>
    /// <param name="id">The toggle's id.</param>
    bool IsOn(string id);

    /// <summary>Changes a text line of the card.</summary>
    /// <param name="id">The text's id.</param>
    /// <param name="text">What it says now.</param>
    void SetText(string id, string text);

    /// <summary>
    /// What to draw over the area until the next call (empty: nothing): shapes in the world's frame
    /// (<see cref="Shape.WorldSpace"/> is taken as true), drawn see-through over everything.
    /// </summary>
    /// <param name="shapes">The shapes.</param>
    void Preview(IReadOnlyList<Shape> shapes);

    /// <summary>A line in the editor's status bar.</summary>
    /// <param name="text">The line.</param>
    void Status(string text);

    /// <summary>
    /// Makes the edits one step of the editor's history (undone and redone as one, saved and applied live like the
    /// editor's own); nothing when they change nothing.
    /// </summary>
    /// <param name="label">The step's name in the history.</param>
    /// <param name="edit">The objects added, changed and removed.</param>
    void Commit(string label, ToolEdit edit);
}

/// <summary>Objects a tool adds, changes and removes in one step (<see cref="IToolContext.Commit"/>).</summary>
public sealed class ToolEdit
{
    private readonly List<(string Prefab, Vector3 Position, Vector3 Rotation, System.Action<ICopy>? Data)> _added = [];
    private readonly List<(IObjectView Object, System.Action<ICopy> Change)> _changed = [];
    private readonly List<IObjectView> _removed = [];

    /// <summary>The objects added.</summary>
    public IReadOnlyList<(string Prefab, Vector3 Position, Vector3 Rotation, System.Action<ICopy>? Data)> Added => _added;

    /// <summary>The objects changed.</summary>
    public IReadOnlyList<(IObjectView Object, System.Action<ICopy> Change)> Changed => _changed;

    /// <summary>The objects removed.</summary>
    public IReadOnlyList<IObjectView> Removed => _removed;

    /// <summary>Adds an object.</summary>
    /// <param name="prefab">Its prefab, as the mod's code names it.</param>
    /// <param name="position">Where, in world metres.</param>
    /// <param name="rotation">Its turn, Unity Euler angles in degrees.</param>
    /// <param name="data">Sets its data (null: none).</param>
    public ToolEdit Add(string prefab, Vector3 position, Vector3 rotation, System.Action<ICopy>? data = null)
    {
        _added.Add((prefab, position, rotation, data));
        return this;
    }

    /// <summary>Changes an object's data, keeping the object (its place, its id in the game where the editor can).</summary>
    /// <param name="obj">The object, as the area or the pointer gave it.</param>
    /// <param name="change">Sets its new values.</param>
    public ToolEdit Change(IObjectView obj, System.Action<ICopy> change)
    {
        _changed.Add((obj, change));
        return this;
    }

    /// <summary>Removes an object.</summary>
    /// <param name="obj">The object, as the area or the pointer gave it.</param>
    public ToolEdit Remove(IObjectView obj)
    {
        _removed.Add(obj);
        return this;
    }
}
