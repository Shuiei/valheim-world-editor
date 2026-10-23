using System.Diagnostics;
using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// The mod's models, read from the player's own copy of the mod (meshes/ beside its plugin, the first plugin the
/// editor's BepInEx folders hold), each once and kept, for every thread drawing at once. The plugin is looked for again
/// every few seconds, and a model whose file changed since (a mod update) is read again; what cannot be read is told
/// once in the log and not drawn.
/// </summary>
/// <param name="host">The editor, to find the mod's plugin.</param>
/// <param name="log">Where what cannot be read is told.</param>
public sealed class ModelLibrary(IEditorHost host, Action<string> log)
{
    /// <summary>How long the mod's folder found is trusted before it is looked for again.</summary>
    private static readonly TimeSpan LookAgain = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly Stopwatch _since = new();
    private readonly Dictionary<string, Kept> _models = new(StringComparer.Ordinal);
    private readonly HashSet<string> _told = new(StringComparer.Ordinal);
    private string? _folder;

    /// <summary>A model as read (null: it could not be), with the stamp of the file it was read from.</summary>
    private sealed record Kept(RailModel? Model, DateTime Written, long Length);

    /// <summary>A model of the mod, by name; null when the mod's file of it is missing or cannot be read.</summary>
    /// <param name="name">The model's name (its file's, without .bfm).</param>
    public RailModel? Get(string name)
    {
        lock (_gate)
        {
            if (!_since.IsRunning || _since.Elapsed >= LookAgain)
            {
                Refresh();
            }
            if (_folder == null)
            {
                return null;
            }
            if (!_models.TryGetValue(name, out var kept))
            {
                _models[name] = kept = Read(Path.Combine(_folder, name + ".bfm"));
            }
            return kept.Model;
        }
    }

    /// <summary>
    /// Looks for the mod's folder again: another folder (the mod moved, or another profile's comes first now) forgets
    /// every model, a file changed forgets its own.
    /// </summary>
    private void Refresh()
    {
        _since.Restart();
        string? folder = host.FindPlugin(RailroadsMod.DllName) is { } plugin
            ? Path.Combine(Path.GetDirectoryName(plugin.Dll) ?? "", RailroadsMod.MeshFolder)
            : null;
        if (folder != _folder)
        {
            _folder = folder;
            _models.Clear();
            return;
        }
        foreach (var model in _models.ToList())
        {
            var file = new FileInfo(Path.Combine(_folder ?? "", model.Key + ".bfm"));
            if (!file.Exists || file.LastWriteTimeUtc != model.Value.Written || file.Length != model.Value.Length)
            {
                _models.Remove(model.Key);
            }
        }
    }

    /// <summary>Reads one model file; null in its place, told once, when it is missing or cannot be read.</summary>
    private Kept Read(string path)
    {
        var file = new FileInfo(path);
        try
        {
            return new Kept(ModelFile.Read(File.ReadAllBytes(path)), file.LastWriteTimeUtc, file.Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (_told.Add(path + ": " + e.Message))
            {
                log($"cannot read Railroads' model {path} ({e.GetType().Name}: {e.Message}): its pieces are drawn without it");
            }
            file.Refresh();
            return new Kept(null, file.Exists ? file.LastWriteTimeUtc : default, file.Exists ? file.Length : -1);
        }
    }
}
