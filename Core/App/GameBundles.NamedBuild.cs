using System.Text.Json;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// Building the names index: each named asset's bundle and path id.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// The names: there already, read from their kept file while the bundles are unchanged, else read from the bundles
    /// (and kept).
    /// </summary>
    private Dictionary<string, List<Where>> EnsureNamed()
    {
        if (_named != null)
        {
            return _named;
        }
        string now = JsonSerializer.Serialize(BundlesNow());
        string? file = NamesFile;
        if (file != null && File.Exists(file))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.GetProperty("Bundles").GetRawText().Replace(" ", "", StringComparison.Ordinal) == now)
                {
                    var kept = new Dictionary<string, List<Where>>();
                    foreach (var p in doc.RootElement.GetProperty("Names").EnumerateObject())
                    {
                        kept[p.Name] = [.. p.Value.EnumerateArray().Select(k => Where.FromKey(k.GetString() ?? "")).OfType<Where>()];
                    }
                    return _named = kept;
                }
            }
            catch (Exception e) when (e is JsonException or IOException or KeyNotFoundException or InvalidOperationException)
            {
                Log.Caught(e, "reading the game files' names cache (the bundles are read again)");
            }
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var named = new Dictionary<string, List<Where>>();
        foreach (string bundle in _files.Keys.Order(StringComparer.Ordinal))
        {
            // A bundle that cannot be read (a mod's, damaged or unusual) is skipped: the others' names still count.
            BundleFileInstance? b = null;
            AssetsFileInstance? a = null;
            try
            {
                b = LoadBundle(_files[bundle]);
                a = _am.LoadAssetsFileFromBundle(b, 0, false);
                var reader = a.file.Reader;
                foreach (var info in a.file.AssetInfos)
                {
                    if (Array.IndexOf(NamedKinds, (AssetClassID)info.TypeId) < 0)
                    {
                        continue;
                    }
                    // m_Name is a named object's first field: nothing else read.
                    reader.Position = info.GetAbsoluteByteOffset(a.file);
                    int n = reader.ReadInt32();
                    if (n is <= 0 or > 256)
                    {
                        continue;
                    }
                    string key = $"{info.TypeId}:{System.Text.Encoding.UTF8.GetString(reader.ReadBytes(n))}";
                    if (!named.TryGetValue(key, out var list))
                    {
                        named[key] = list = [];
                    }
                    list.Add(new Where(bundle, info.PathId));
                }
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or EndOfStreamException
                or IndexOutOfRangeException or ArgumentException or NullReferenceException or OverflowException)
            {
                Log.Caught(e, $"reading the names in the bundle {bundle} (skipped)");
            }
            finally
            {
                // Closed again, read whole or not (a file kept open could not be replaced later), unless kept for
                // reading its assets.
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
        Console.WriteLine($"game files: {named.Count} mesh, material and texture names read from {_files.Count} bundles in {sw.ElapsedMilliseconds} ms");
        if (file != null)
        {
            try
            {
                SafeFile.WriteAllText(file, JsonSerializer.Serialize(new { Bundles = BundlesNow(), Names = named.ToDictionary(kv => kv.Key, kv => kv.Value.Select(w => w.Key)) }));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Caught(e, "keeping the game files' names (read again next time)");
            }
        }
        return _named = named;
    }
}
