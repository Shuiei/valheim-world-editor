using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// The game's bundles, as <c>GameCatalogues</c> opens and visits them.
/// </summary>
public static partial class GameCatalogues
{
    /// <summary>
    /// The game's bundles, opened one at a time (all of them at once do not fit in memory).
    /// </summary>
    internal sealed partial class Game : IDisposable
    {
        /// <summary>
        /// AssetsTools.NET's manager for every bundle opened here.
        /// </summary>
        private readonly AssetsManager _am = new() { UseTemplateFieldCache = true, UseMonoTemplateFieldCache = true, UseRefTypeManagerCache = true, UseQuickLookup = true };
        /// <summary>
        /// The bundles folder.
        /// </summary>
        private readonly string _dir;

        /// <summary>
        /// The bundles' file names, in order.
        /// </summary>
        public List<string> Bundles { get; }

        /// <summary>
        /// Each internal file (lower case) -&gt; its bundle: filled as the bundles are read.
        /// </summary>
        public Dictionary<string, string> CabBundle { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The bundles of a bundles folder (every file in it, by name).
        /// </summary>
        public Game(string dir)
        {
            _dir = dir;
            Bundles = [.. Directory.GetFiles(dir).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];
        }

        /// <summary>
        /// The bundles open now, by file name. AssetsTools keeps one instance per path, so a bundle asked for
        /// while open is the same one, given to one more user: its files are closed when the last one is done,
        /// not when a second wrapper of the same instance would be.
        /// </summary>
        private readonly Dictionary<string, Bundle> _open = new(StringComparer.Ordinal);

        /// <summary>
        /// A bundle, or null when it is not one. Its internal files are added to <c>CabBundle</c>. One open
        /// already is the same bundle, open until every user has disposed it.
        /// </summary>
        public Bundle? Open(string name)
        {
            if (_open.TryGetValue(name, out var open))
            {
                return open.Share();
            }
            BundleFileInstance? file = null;
            try
            {
                file = Load(name);
                var b = new Bundle(_am, file, () => _open.Remove(name));
                foreach (var f in b.Files)
                {
                    CabBundle[f.Cab] = name;
                }
                _open[name] = b;
                return b;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
            {
                // A bundle that opened but whose files could not be read is closed again at once, with the files read
                // from it so far (it stayed open until the end of the scan).
                Log.Caught(e, $"opening the bundle {name} (closed again, not read)");
                if (file != null)
                {
                    foreach (var assets in file.loadedAssetsFiles.ToList())
                    {
                        _am.UnloadAssetsFile(assets);
                    }
                    _am.UnloadBundleFile(file);
                }
                return null;
            }
        }

        /// <summary>
        /// Every bundle's internal files, from their headers only (what Open fills as it goes): for
        /// references into bundles not opened yet.
        /// </summary>
        public void IndexCabs()
        {
            foreach (string name in Bundles)
            {
                try
                {
                    var b = Load(name);
                    foreach (var d in b.file.BlockAndDirInfo.DirectoryInfos)
                    {
                        CabBundle[d.Name.ToLowerInvariant()] = name;
                    }
                    _am.UnloadBundleFile(b);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
                {
                    // Not a bundle.
                    Log.CaughtBrief(e, $"listing the files in {name} (not a bundle: passed over)");
                }
            }
        }

        /// <summary>
        /// Loads a bundle file of the folder, read in place. A file that is not a bundle fails to load, and its stream
        /// is closed before the error goes on: AssetsTools leaves it open, and Windows then refuses to delete or
        /// replace the folder (as GameBundles does for the same reason).
        /// </summary>
        /// <param name="name">The bundle's file name.</param>
        private BundleFileInstance Load(string name)
        {
            string path = Path.GetFullPath(Path.Combine(_dir, name));
            var stream = File.OpenRead(path);
            try
            {
                return _am.LoadBundleFile(stream, path, false);
            }
            catch
            {
                // Not a bundle (said by the caller): the file is let go of.
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Each reference's object, read in the bundle that has its file (each bundle opened once);
        /// references to files no bundle has are left out.
        /// </summary>
        public void Visit(IEnumerable<Ref> refs, Action<Bundle, Bundle.Inner, long> visit)
        {
            foreach (var group in refs.Distinct().Where(r => CabBundle.ContainsKey(r.Cab)).GroupBy(r => CabBundle[r.Cab]).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                using var b = Open(group.Key);
                if (b == null)
                {
                    continue;
                }
                foreach (var r in group)
                {
                    if (b.Files.FirstOrDefault(f => f.Cab == r.Cab) is { } inner)
                    {
                        visit(b, inner, r.PathId);
                    }
                }
            }
        }

        /// <summary>
        /// Closes the bundles kept open for scripts and everything else the manager still holds.
        /// </summary>
        public void Dispose()
        {
            foreach (var b in _scriptBundles.Values)
            {
                b?.Dispose();
            }
            _am.UnloadAllAssetsFiles(true);
            _am.UnloadAllBundleFiles();
        }
    }
}
