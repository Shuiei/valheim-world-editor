using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// A bundle open for a scan: its internal files, and the objects read from them.
/// </summary>
public static partial class GameCatalogues
{
    /// <summary>
    /// A bundle open for a scan: its internal files with objects (scene bundles have two), and every
    /// object read once (kept until the bundle is closed).
    /// </summary>
    internal sealed partial class Bundle : IDisposable
    {
        /// <summary>
        /// The manager the bundle was opened with.
        /// </summary>
        private readonly AssetsManager _am;
        /// <summary>
        /// The bundle itself.
        /// </summary>
        private readonly BundleFileInstance _bundle;
        /// <summary>
        /// Objects by path id over the whole bundle (where two files have the same id: the later file's).
        /// </summary>
        private readonly Dictionary<long, Inner> _byId = [];
        /// <summary>
        /// Objects read once, by file and path id (null: not there or not readable).
        /// </summary>
        private readonly Dictionary<(Inner, long), AssetTypeValueField?> _fields = [];
        /// <summary>
        /// Told once the bundle is closed (its last user disposed it).
        /// </summary>
        private readonly Action _closed;
        /// <summary>
        /// How many have the bundle open: one at first, one more per <c>Share</c>, one fewer per
        /// <c>Dispose</c>, which closes it at none.
        /// </summary>
        private int _users = 1;

        /// <summary>
        /// The internal files with objects, in the bundle's order.
        /// </summary>
        public List<Inner> Files { get; } = [];

        /// <summary>
        /// Wraps a loaded bundle: its reads go through a <c>BlockCache</c>, and each assets file in it is opened.
        /// </summary>
        /// <param name="am">The manager the bundle was loaded with.</param>
        /// <param name="bundle">The bundle.</param>
        /// <param name="closed">Told once the bundle is closed.</param>
        public Bundle(AssetsManager am, BundleFileInstance bundle, Action closed)
        {
            _am = am;
            _bundle = bundle;
            _closed = closed;
            if (bundle.file.DataReader.BaseStream is not BlockCache)
            {
                bundle.file.DataReader = new AssetsFileReader(new BlockCache(bundle.file.DataReader.BaseStream)) { BigEndian = bundle.file.DataReader.BigEndian };
            }
            var dirs = bundle.file.BlockAndDirInfo.DirectoryInfos;
            for (int i = 0; i < dirs.Count; i++)
            {
                if (!bundle.file.IsAssetsFile(i))
                {
                    continue;
                }
                var inner = new Inner(dirs[i].Name.ToLowerInvariant(), am.LoadAssetsFileFromBundle(bundle, i, false));
                Files.Add(inner);
                foreach (var info in inner.Assets.file.AssetInfos)
                {
                    _byId[info.PathId] = inner;
                }
            }
        }

        /// <summary>
        /// Every object, in the bundle's order.
        /// </summary>
        public IEnumerable<(Inner File, AssetFileInfo Info)> Objects => Files.SelectMany(f => f.Assets.file.AssetInfos.Select(i => (f, i)));

        /// <summary>
        /// The file that has this path id (where two have it: the later one).
        /// </summary>
        public Inner? FileWith(long pid) => _byId.GetValueOrDefault(pid);

        /// <summary>
        /// A GameObject's components as (type, path id), nothing read.
        /// </summary>
        public IEnumerable<(int Type, long Pid)> ComponentIds(AssetTypeValueField go) =>
            go["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong).Select(pid => (TypeOf(pid), pid)).Where(c => c.Item1 >= 0);

        /// <summary>
        /// By path id anywhere in the bundle.
        /// </summary>
        public AssetTypeValueField? Field(long pid) => _byId.TryGetValue(pid, out var f) ? Field(f, pid) : null;

        /// <summary>
        /// An object's Unity class id in a file, -1 when it is not there.
        /// </summary>
        public static int TypeOf(Inner f, long pid) => f.Assets.file.GetAssetInfo(pid)?.TypeId ?? -1;

        /// <summary>
        /// An object's Unity class id anywhere in the bundle, -1 when it is not there.
        /// </summary>
        public int TypeOf(long pid) => _byId.TryGetValue(pid, out var f) ? TypeOf(f, pid) : -1;

        /// <summary>
        /// The bundle for one more user: it stays open until that one has disposed it too.
        /// </summary>
        public Bundle Share()
        {
            _users++;
            return this;
        }

        /// <summary>
        /// One user fewer; the last one's unloads the bundle's assets files and the bundle.
        /// </summary>
        public void Dispose()
        {
            if (_users == 0 || --_users > 0)
            {
                return;
            }
            foreach (var f in Files)
            {
                _am.UnloadAssetsFile(f.Assets);
            }
            _am.UnloadBundleFile(_bundle);
            _closed();
        }
    }
}
