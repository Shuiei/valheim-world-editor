using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// Reading one bundle's table of contents for the index: the prefabs and the objects it holds.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// A bundle's table of contents: each asset's path and path id, in its order (empty without type trees, which
    /// describe the table too). Null for a file that is not a bundle, and for a bundle that cannot be read (no assets
    /// file first, or a table that cannot be read), which is said in the log. The bundle's internal file names are
    /// added to <c>_cab</c>, and it is closed again unless kept open.
    /// </summary>
    private List<(string Path, long PathId)>? ReadContents(string bundle)
    {
        BundleFileInstance? b = null;
        AssetsFileInstance? a = null;
        try
        {
            try
            {
                b = _am.LoadBundleFile(_files[bundle], false);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException)
            {
                Log.CaughtBrief(e, $"opening the bundle {bundle} (not a bundle read here: skipped)");
                return null;
            }
            foreach (var d in b.file.BlockAndDirInfo.DirectoryInfos)
            {
                _cab[d.Name.Split('.')[0]] = bundle;
            }
            a = _am.LoadAssetsFileFromBundle(b, 0, false);
            if (a == null)
            {
                Console.WriteLine($"game files: {bundle} does not start with an assets file, skipped");
                return null;
            }
            var contents = new List<(string, long)>();
            var ab = a.file.Metadata.TypeTreeEnabled ? a.file.GetAssetsOfType(AssetClassID.AssetBundle).FirstOrDefault() : null;
            if (ab != null)
            {
                foreach (var c in _am.GetBaseField(a, ab)["m_Container.Array"].Children)
                {
                    contents.Add((c["first"].AsString, c["second.asset.m_PathID"].AsLong));
                }
            }
            return contents;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or EndOfStreamException
            or IndexOutOfRangeException or ArgumentException or NullReferenceException or OverflowException)
        {
            Log.Caught(e, $"reading the contents of the bundle {bundle} (skipped)");
            return null;
        }
        finally
        {
            // Closed again, read whole or not (a file kept open could not be replaced later), unless kept for reading
            // its assets.
            if (!_open.ContainsKey(bundle))
            {
                if (a != null)
                {
                    _am.UnloadAssetsFile(a);
                }
                if (b != null)
                {
                    _am.UnloadBundleFile(b);
                }
            }
        }
    }
}
