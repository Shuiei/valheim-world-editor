using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// The index of the game's bundles: built once, kept in a file, and read back when the bundles change.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// The kept index's shape (the file is written from an anonymous object with an Assets map too).
    /// </summary>
    private sealed record IndexFile(List<object[]> Bundles, Dictionary<string, string> Prefabs);

    /// <summary>
    /// The bundles as [name, size, last write (Unix seconds)], ordered by name: the kept index is used only while they
    /// match.
    /// </summary>
    private List<object[]> BundlesNow() => [.. _files.Keys.Order(StringComparer.Ordinal).Select(n =>
    {
        var fi = new FileInfo(_files[n]);
        return new object[] { n, fi.Length, new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds() };
    })];

    /// <summary>
    /// Makes sure the index is there (see <c>Index</c>), telling how far building it is: bundles done, of all of
    /// them. Nothing is told when it is there already or read from the kept file.
    /// </summary>
    public void EnsureIndexed(Action<int, int>? progress)
    {
        lock (_sync)
        {
            EnsureIndex(progress);
        }
    }

    /// <summary>
    /// The index: already there, read from the kept file when the bundles have not changed, else built (and kept).
    /// </summary>
    /// <param name="progress">Told the bundles done, of all of them, while it is built.</param>
    private Dictionary<string, Where> EnsureIndex(Action<int, int>? progress = null)
    {
        if (_index != null)
        {
            return _index;
        }
        string now = JsonSerializer.Serialize(BundlesNow());
        if (_cacheFile != null && File.Exists(_cacheFile))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_cacheFile));
                if (doc.RootElement.GetProperty("Bundles").GetRawText().Replace(" ", "", StringComparison.Ordinal) == now)
                {
                    var res = new Dictionary<string, Where>();
                    foreach (var p in doc.RootElement.GetProperty("Prefabs").EnumerateObject())
                    {
                        if (Where.FromKey(p.Value.GetString() ?? "") is Where w)
                        {
                            res[p.Name] = w;
                        }
                    }
                    var assets = new Dictionary<string, Where>();
                    foreach (var p in doc.RootElement.GetProperty("Assets").EnumerateObject())
                    {
                        if (Where.FromKey(p.Value.GetString() ?? "") is Where w)
                        {
                            assets[p.Name] = w;
                        }
                    }
                    _assets = assets;
                    return SetIndex(res);
                }
            }
            catch (Exception e) when (e is JsonException or IOException or KeyNotFoundException or InvalidOperationException)
            {
                Log.Caught(e, "reading the game files' index cache (the bundles are read again)");
            }
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var index = BuildIndex(progress);
        Console.WriteLine($"game files: {index.Count} prefabs in {_files.Count} bundles indexed in {sw.ElapsedMilliseconds} ms");
        if (_cacheFile != null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile)!);
                SafeFile.WriteAllText(_cacheFile, JsonSerializer.Serialize(new { Bundles = BundlesNow(), Prefabs = index.ToDictionary(kv => kv.Key, kv => kv.Value.Key), Assets = _assets.ToDictionary(kv => kv.Key, kv => kv.Value.Key) }));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Caught(e, "keeping the game files' index (made again next time)");
            }
        }
        return SetIndex(index);
    }

    /// <summary>
    /// Sets the index and its by-hash map.
    /// </summary>
    private Dictionary<string, Where> SetIndex(Dictionary<string, Where> index)
    {
        _byHash = [];
        foreach (string n in index.Keys)
        {
            _byHash.TryAdd(StableHash.Of(n), n);
        }
        return _index = index;
    }
}
