using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// Opening a bundle and reading its objects' fields, with the internal files' references.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// A bundle and its assets file, opened on first use and kept open.
    /// </summary>
    private (BundleFileInstance Bundle, AssetsFileInstance Assets) Open(string bundle)
    {
        if (!_open.TryGetValue(bundle, out var o))
        {
            var b = LoadBundle(_files[bundle]);
            o = (b, _am.LoadAssetsFileFromBundle(b, 0, false));
            _open[bundle] = o;
        }
        return o;
    }

    /// <summary>
    /// Loads a bundle file, read in place: LZ4 blocks are unpacked as they are read (unpacking all of it is no faster;
    /// the time goes into reading objects' fields). A file that is not a bundle fails to load, and its stream is closed
    /// before the error goes on: AssetsTools leaves it open, and Windows then refuses to delete or replace the folder.
    /// </summary>
    /// <param name="path">The bundle file.</param>
    private BundleFileInstance LoadBundle(string path)
    {
        var stream = File.OpenRead(path);
        try
        {
            return _am.LoadBundleFile(stream, Path.GetFullPath(path), false);
        }
        catch
        {
            // Not a bundle (said by the caller): the file is let go of.
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Fills the CAB name map from every bundle, when the index was read from the kept file and did not build it.
    /// </summary>
    private void EnsureCabs()
    {
        if (_cabsFull)
        {
            return;
        }
        foreach (var (name, path) in _files)
        {
            // A bundle kept open is read as it is: loading it again opened its file a second time, never closed.
            bool kept = _open.TryGetValue(name, out var open);
            BundleFileInstance b;
            if (kept)
            {
                b = open.Bundle;
            }
            else
            {
                // A file that is not a bundle is passed over, as the index does.
                try
                {
                    b = LoadBundle(path);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException)
                {
                    Log.CaughtBrief(e, $"opening {name} (not a bundle: passed over)");
                    continue;
                }
            }
            try
            {
                foreach (var d in b.file.BlockAndDirInfo.DirectoryInfos)
                {
                    _cab[d.Name.Split('.')[0]] = name;
                }
            }
            // One bundle that cannot be read is said and passed over: the others' names are still taken.
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or EndOfStreamException
                or IndexOutOfRangeException or ArgumentException or NullReferenceException or OverflowException)
            {
                Log.Caught(e, $"listing the files in the bundle {name} (skipped)");
            }
            finally
            {
                if (!kept)
                {
                    _am.UnloadBundleFile(b);
                }
            }
        }
        _cabsFull = true;
    }

    /// <summary>
    /// An object's fields, null when the bundle has no such object or does not describe its objects' fields (no type
    /// trees: the editor has no class database to read them by); kept in <c>_fields</c> until the read is over.
    /// </summary>
    private AssetTypeValueField? Read(string bundle, long pathId)
    {
        if (_fields.TryGetValue((bundle, pathId), out var cached))
        {
            return cached;
        }
        var (_, a) = Open(bundle);
        var info = a.file.GetAssetInfo(pathId);
        var f = info == null || !a.file.Metadata.TypeTreeEnabled ? null : _am.GetBaseField(a, info);
        _fields[(bundle, pathId)] = f;
        return f;
    }
}
