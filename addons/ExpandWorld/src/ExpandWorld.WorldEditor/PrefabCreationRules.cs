using ValheimWorldEditor.Sdk;
using static ExpandWorld.WorldEditor.ExpandWorldMod;

namespace ExpandWorld.WorldEditor;

/// <summary>
/// Expand World Prefabs' "create" rules as the editor makes objects (ICreationRules): those a step adds. The rules and data entries are read from the first BepInEx folder with the mod, again when they are more than
/// a few seconds old (the hook is called from several threads: they are read under a lock).
/// </summary>
/// <param name="host">The editor (its BepInEx folders).</param>
internal sealed class PrefabCreationRules(IEditorHost host) : ICreationRules
{
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(10);

    private readonly Lock _lock = new();

    private (IReadOnlyList<PrefabRule> Rules, HashSet<string> Prefabs, IReadOnlyDictionary<string, CustomData> Entries)? _read;

    private DateTime _readAt;

    /// <summary>The rules that act as objects are made, their objects, and the data entries; read again when old.</summary>
    private (IReadOnlyList<PrefabRule> Rules, HashSet<string> Prefabs, IReadOnlyDictionary<string, CustomData> Entries) Read()
    {
        lock (_lock)
        {
            if (_read is { } read && DateTime.UtcNow - _readAt < Fresh)
            {
                return read;
            }
            var file = ReadPrefabRules(host.BepInExFolders);
            List<PrefabRule> rules = [.. file.Rules.Where(r => r.Create != null)];
            var entries = rules.Count > 0
                ? ReadCustomObjects(host.BepInExFolders).Entries.ToDictionary(e => e.Name, StringComparer.Ordinal)
                : [];
            _read = (rules, rules.SelectMany(r => r.Prefabs).ToHashSet(StringComparer.Ordinal), entries);
            _readAt = DateTime.UtcNow;
            return _read.Value;
        }
    }

    /// <inheritdoc/>
    public bool Covers(string prefab) => Read().Prefabs.Contains(prefab);

    /// <inheritdoc/>
    public void Created(ICreation made)
    {
        var (rules, _, entries) = Read();
        ApplyCreateRules(made, rules, entries);
    }
}
