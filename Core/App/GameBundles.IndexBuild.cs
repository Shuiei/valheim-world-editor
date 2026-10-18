namespace TerrainEditor.App;

/// <summary>
/// Building the bundles' index: each bundle's contents read from its table of contents.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// Prefab name -&gt; its root, from each bundle's table of contents (prefabs only). A name in several
    /// bundles (a model asset and the prefab made from it): the one with a ZNetView, the prefab the game
    /// spawns.
    /// </summary>
    /// <param name="progress">Told the bundles done, of all of them, before each.</param>
    private Dictionary<string, Where> BuildIndex(Action<int, int>? progress)
    {
        var res = new Dictionary<string, Where>();
        var twice = new Dictionary<string, List<Where>>();
        var assets = new Dictionary<string, Where>();
        int done = 0;
        foreach (string bundle in _files.Keys.Order(StringComparer.Ordinal))
        {
            progress?.Invoke(done++, _files.Count);
            // A file that is not a bundle is passed over; a bundle that cannot be read (a mod's, damaged or unusual)
            // is skipped and said, none of its entries kept: the others' prefabs are still indexed.
            if (ReadContents(bundle) is not { } contents)
            {
                continue;
            }
            foreach (var (path, pid) in contents)
            {
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    string file = Path.GetFileName(path);
                    if (Array.IndexOf(NamedAssets, file) >= 0)
                    {
                        assets.TryAdd(file, new Where(bundle, pid));
                    }
                    continue;
                }
                string n = Path.GetFileNameWithoutExtension(path);
                var w = new Where(bundle, pid);
                if (!res.TryAdd(n, w))
                {
                    if (!twice.TryGetValue(n, out var l))
                    {
                        twice[n] = l = [res[n]];
                    }
                    l.Add(w);
                }
            }
        }
        // Every bundle's contents were read, and with them its names.
        _cabsFull = true;
        foreach (var (n, list) in twice)
        {
            res[n] = list.FirstOrDefault(IsNetworked) ?? list[0];
        }
        _fields.Clear();
        _assets = assets;
        return res;
    }
}
