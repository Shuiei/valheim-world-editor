using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// The bundles open now: which are there, opened and closed, and their monobehaviours.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// Whether the folder has this bundle (by file name).
    /// </summary>
    public bool HasBundle(string name) => _files.ContainsKey(name);

    /// <summary>
    /// The bundle's file, for what is read from its size and date.
    /// </summary>
    public string? PathOf(string name) => _files.GetValueOrDefault(name);

    /// <summary>
    /// Every MonoBehaviour of a bundle, its fields read whole (not kept): data the game keeps on its scene
    /// objects, such as the location rules (GameLocations). Objects that cannot be read are skipped.
    /// </summary>
    public List<AssetTypeValueField> MonoBehaviours(string bundle)
    {
        lock (_sync)
        {
            var (_, a) = Open(bundle);
            var list = new List<AssetTypeValueField>();
            Exception? first = null;
            int unread = 0;
            // Without type trees no script's fields are described: none read.
            foreach (var info in a.file.Metadata.TypeTreeEnabled ? a.file.GetAssetsOfType(AssetClassID.MonoBehaviour) : [])
            {
                try
                {
                    list.Add(_am.GetBaseField(a, info));
                }
                catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
                {
                    // A script whose fields are not described (no type tree): not one of ours. Said once below for
                    // the bundle, as there can be hundreds.
                    first ??= e;
                    unread++;
                }
            }
            if (first != null)
            {
                Log.CaughtBrief(first, $"reading the fields of {unread} script(s) in the bundle {bundle} (not described: not ours, skipped)");
            }
            return list;
        }
    }

    /// <summary>How many bundles are open now (each kept open from its first use until <see cref="Close"/>).</summary>
    internal int OpenBundles
    {
        get
        {
            lock (_sync)
            {
                return _open.Count;
            }
        }
    }

    /// <summary>
    /// Closes every bundle the reader keeps open (its file streams with it) and forgets the fields read from them:
    /// what a folder about to be deleted or copied over needs, since Windows refuses that while a file in it is open.
    /// The reader stays usable: a bundle is opened again on its next use.
    /// </summary>
    public void Close()
    {
        lock (_sync)
        {
            foreach (var (b, a) in _open.Values)
            {
                _am.UnloadAssetsFile(a);
                _am.UnloadBundleFile(b);
            }
            _open.Clear();
            _fields.Clear();
        }
    }
}
