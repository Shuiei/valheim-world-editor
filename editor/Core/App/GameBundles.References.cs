using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// Following references between objects: their types, the fields they name, and the components of a game object.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// An object's Unity class id, -1 when it is not there.
    /// </summary>
    private int TypeOf(string bundle, long pathId) => Open(bundle).Assets.file.GetAssetInfo(pathId)?.TypeId ?? -1;

    /// <summary>
    /// Whether a field is present (AssetsTools gives a dummy for a missing one).
    /// </summary>
    private static bool Has(AssetTypeValueField f, string name) => !f[name].IsDummy;

    /// <summary>
    /// A reference (m_FileID, m_PathID) made in a bundle: where it points (another bundle for m_FileID &gt; 0).
    /// </summary>
    private Where? Resolve(string bundle, AssetTypeValueField pptr)
    {
        long pid = pptr["m_PathID"].AsLong;
        if (pid == 0)
        {
            return null;
        }
        int file = pptr["m_FileID"].AsInt;
        if (file == 0)
        {
            return new Where(bundle, pid);
        }
        var externals = Open(bundle).Assets.file.Metadata.Externals;
        if (file - 1 >= externals.Count)
        {
            return null;
        }
        EnsureCabs();
        string cab = externals[file - 1].PathName.Split('/').Last().Split('.')[0];
        return _cab.TryGetValue(cab, out string? b) ? new Where(b, pid) : null;
    }

    /// <summary>
    /// A GameObject's components that can be read, with their class id and path id.
    /// </summary>
    private IEnumerable<(int Type, long Pid, AssetTypeValueField F)> Components(string bundle, AssetTypeValueField go)
    {
        foreach (var c in go["m_Component.Array"].Children)
        {
            long pid = c["component.m_PathID"].AsLong;
            int t = TypeOf(bundle, pid);
            if (t >= 0 && Read(bundle, pid) is { } f)
            {
                yield return (t, pid, f);
            }
        }
    }
}
