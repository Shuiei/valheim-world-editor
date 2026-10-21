using System.Collections.Generic;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// What the editor read of the game's own files for the extensions (see <see cref="IEditorHost.Game"/>): the fields of
/// the game's scripts a mod's worldeditor.json asks for ("gameComponents"), read from the player's Valheim. Ready once a
/// world or the Workshop is open: ask it from the hooks, not from <see cref="IWorldEditorExtension.Register"/>.
/// </summary>
public interface IGameData
{
    /// <summary>
    /// Every component of a game script in the game's prefabs, in the order the editor found them; empty for a script
    /// no running extension's manifest asks for, or before a world is open.
    /// </summary>
    /// <param name="scriptClass">The script's class name, as the game's code names it ("TerrainOp").</param>
    IReadOnlyList<GameComponent> Components(string scriptClass);
}

/// <summary>One component of a game script, as the game's prefab holds it.</summary>
public sealed class GameComponent
{
    /// <summary>A component's values.</summary>
    /// <param name="prefab">The prefab it is in: its root object's name.</param>
    /// <param name="objectName">The object it is on (the root's own name when it is on the root).</param>
    /// <param name="fields">Its serialised fields by name (see <see cref="Fields"/>).</param>
    public GameComponent(string prefab, string objectName, IReadOnlyDictionary<string, object?> fields)
    {
        Prefab = prefab;
        ObjectName = objectName;
        Fields = fields;
    }

    /// <summary>The prefab it is in: its root object's name.</summary>
    public string Prefab { get; }

    /// <summary>The object it is on (the prefab's own name when it is on the root, a child's name otherwise).</summary>
    public string ObjectName { get; }

    /// <summary>
    /// Its fields as the game saves them, by name ("m_settings"): numbers as <c>long</c> or <c>double</c>, yes/no as
    /// <c>long</c> 0 or 1, texts as <c>string</c>, lists as <c>IReadOnlyList&lt;object?&gt;</c>, structures (and
    /// references to other objects, with their <c>m_FileID</c> and <c>m_PathID</c>) as
    /// <c>IReadOnlyDictionary&lt;string, object?&gt;</c>. Unity's own fields (m_GameObject, m_Enabled, m_Script...)
    /// are left out.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Fields { get; }
}
