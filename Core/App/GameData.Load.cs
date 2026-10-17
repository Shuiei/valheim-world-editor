namespace TerrainEditor.App;

// Reading every kind from the game (its own readers, or the one pass over the bundles), from the caches when they are current (see GameData).
public static partial class GameData
{
    /// <summary>
    /// Every kind as JSON, by name: from its cache when the game and the editor are unchanged, else read
    /// from the game (and kept, with cache); null when one cannot be read.
    /// </summary>
    /// <param name="bundles">The game's bundles.</param>
    /// <param name="cache">From and to the caches.</param>
    /// <param name="progress">Told how far the reading is (0..1) and what it reads: the kinds with a reader of their
    /// own share the first part (a fifth when the one pass over the bundles follows, which takes most of the time),
    /// the pass the rest, bundle by bundle.</param>
    internal static Dictionary<string, string>? Load(GameBundles bundles, bool cache, Action<double, string>? progress = null)
    {
        string valheim = ValheimOf(bundles.Folder);
        string what = "files";
        try
        {
            string stamp = Stamp(valheim, bundles.Folder);
            var json = new Dictionary<string, string>();
            var read = new List<string>();
            var own = new List<Kind>();
            var pass = new List<string>();
            foreach (var kind in Kinds)
            {
                if (cache && Cached(CacheFile(kind.Name), kind.Name + " " + stamp) is { } cached)
                {
                    Console.WriteLine($"Game data {kind.Name}: from the cache.");
                    json[kind.Name] = cached;
                }
                else if (kind.Read == null)
                {
                    pass.Add(kind.Name);
                }
                else
                {
                    own.Add(kind);
                }
            }
            // No kind of its own left to read (all from the caches): the pass starts at nothing read.
            double ownShare = own.Count == 0 ? 0 : pass.Count > 0 ? 0.2 : 1;
            for (int i = 0; i < own.Count; i++)
            {
                var kind = own[i];
                what = kind.Name;
                progress?.Invoke(ownShare * i / own.Count, $"Reading the game's {kind.Name}");
                var watch = System.Diagnostics.Stopwatch.StartNew();
                json[kind.Name] = kind.Read!(valheim, bundles);
                Console.WriteLine($"Game data {kind.Name}: read from the game in {watch.ElapsedMilliseconds} ms.");
                read.Add(kind.Name);
            }
            if (pass.Count > 0)
            {
                what = string.Join(", ", pass);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                void Bundle(int done, int count) =>
                    progress?.Invoke(ownShare + ((1 - ownShare) * done / Math.Max(1, count)), $"Reading the game's bundles: {done} of {count}");
                foreach (var (name, j) in GameCatalogues.ReadAtStart(bundles, pass, Bundle))
                {
                    json[name] = j;
                }
                Console.WriteLine($"Game data {what}: read from the game's bundles in {watch.ElapsedMilliseconds} ms.");
                read.AddRange(pass);
            }
            if (cache)
            {
                foreach (string name in read)
                {
                    KeepCache(CacheFile(name), name + " " + stamp + "\n" + json[name]);
                }
            }
            return json;
        }
        catch (Exception e)
        {
            // Anything in the game's files can fail the reading: no world opens then.
            Log.Caught(e, $"reading the game's {what} (no world opens)");
            return null;
        }
    }
}
