using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// What the editor reads of the game's files at its start (GameData), in C# on the same reader as the
/// models: the kinds found in the bundles, all in one pass over them (ReadAtStart), and those with their
/// own readers. Each is written as CatalogueJson writes it.
/// </summary>
public static partial class GameCatalogues
{
    /// <summary>
    /// The kinds read in the one pass over the bundles, each found from the scripts it needs up to their
    /// prefabs, not by reading every object of every bundle (seconds, not minutes). Each bundle is opened
    /// once and given to each kind's reader, in the order of their file names: the order entries come in,
    /// and which one wins when a name is found twice.
    /// </summary>
    public static readonly string[] AtStart = ["terrain-modifiers", "prefabs", "pieces", "piece-support", "piece-place", "piece-cost", "dungeon-rooms"];

    /// <summary>
    /// The kinds named (of AtStart), as their JSON. Throws ArgumentException for a kind not in <c>AtStart</c>.
    /// </summary>
    /// <param name="bundles">The game's bundles (its folder is what is read).</param>
    /// <param name="kinds">The kinds to read, of <c>AtStart</c>.</param>
    /// <param name="progress">Told the bundles read, of all of them, before each.</param>
    /// <returns>Each kind's JSON, by kind.</returns>
    public static Dictionary<string, string> ReadAtStart(GameBundles bundles, IReadOnlyList<string> kinds, Action<int, int>? progress = null)
    {
        var scans = kinds.Select(k => (Kind: k, Scan: k switch
        {
            "terrain-modifiers" => (IStartScan)new ModifierScan(),
            "prefabs" => new PrefabScan(),
            "pieces" => new PieceScan(),
            "piece-support" => new SupportScan(),
            "piece-place" => new PlaceScan(),
            "piece-cost" => new CostScan(Localization.English(bundles.Folder)),
            "dungeon-rooms" => new DungeonScan(),
            _ => throw new ArgumentException($"no kind {k} is read at the start"),
        })).ToList();
        using var game = new Game(bundles.Folder);
        game.IndexCabs();
        int done = 0;
        foreach (string name in game.Bundles)
        {
            progress?.Invoke(done++, game.Bundles.Count);
            using var b = game.Open(name);
            if (b != null)
            {
                foreach (var (_, scan) in scans)
                {
                    scan.Read(b, game);
                }
            }
        }
        return scans.ToDictionary(s => s.Kind, s => s.Scan.Json());
    }

    /// <summary>
    /// One kind's reading of every bundle at the editor's start (the game's other bundles are found
    /// through game), and its JSON.
    /// </summary>
    private interface IStartScan
    {
        /// <summary>
        /// Reads what this kind needs of one bundle; <c>game</c> gives the others (scripts, references).
        /// </summary>
        void Read(Bundle b, Game game);

        /// <summary>
        /// The kind's JSON, once every bundle is read.
        /// </summary>
        string Json();
    }

    // ---- The bundles.

    /// <summary>
    /// An object of another internal file of the bundles: the file's name (lower case) and its path id.
    /// </summary>
    /// <param name="Cab">The internal file's name, lower case.</param>
    /// <param name="PathId">The object's path id in that file.</param>
    private readonly record struct Ref(string Cab, long PathId);

    /// <summary>
    /// The game's bundles, opened one at a time (all of them at once do not fit in memory).
    /// </summary>
    private sealed class Game : IDisposable
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
        /// A bundle, or null when it is not one. Its internal files are added to <c>CabBundle</c>.
        /// </summary>
        public Bundle? Open(string name)
        {
            try
            {
                var b = new Bundle(_am, _am.LoadBundleFile(Path.Combine(_dir, name), false));
                foreach (var f in b.Files)
                {
                    CabBundle[f.Cab] = name;
                }
                return b;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
            {
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
                    var b = _am.LoadBundleFile(Path.Combine(_dir, name), false);
                    foreach (var d in b.file.BlockAndDirInfo.DirectoryInfos)
                    {
                        CabBundle[d.Name.ToLowerInvariant()] = name;
                    }
                    _am.UnloadBundleFile(b);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
                {
                    // Not a bundle.
                }
            }
        }

        /// <summary>
        /// The class of each script (MonoScript) met, by where it is (null: could not be told).
        /// </summary>
        private readonly Dictionary<Ref, string?> _scripts = [];
        /// <summary>
        /// The bundles opened for scripts, by file name (null: not a bundle), kept open until the end.
        /// </summary>
        private readonly Dictionary<string, Bundle?> _scriptBundles = new(StringComparer.Ordinal);

        /// <summary>
        /// A MonoBehaviour's class ("TerrainModifier", "ZNetView"...), from the script its file lists for
        /// it: nothing of the object itself is read. Null when it cannot be told.
        /// </summary>
        public string? ScriptClass(Bundle b, Bundle.Inner f, AssetFileInfo info)
        {
            var file = f.Assets.file;
            ushort index = info.GetScriptIndex(file);
            if (index == 0xffff || index >= file.Metadata.ScriptTypes.Count)
            {
                return null;
            }
            var script = file.Metadata.ScriptTypes[index];
            if (f.FileOf(script.FileId) is not string cab)
            {
                return null;
            }
            var r = new Ref(cab, script.PathId);
            if (_scripts.TryGetValue(r, out string? cached))
            {
                return cached;
            }
            Bundle? sb = b.Files.Any(x => x.Cab == cab) ? b : null;
            if (sb == null && CabBundle.TryGetValue(cab, out string? name))
            {
                // Scripts are in a few bundles: kept open until the end.
                if (!_scriptBundles.TryGetValue(name, out sb))
                {
                    _scriptBundles[name] = sb = Open(name);
                }
            }
            var inner = sb?.Files.FirstOrDefault(x => x.Cab == cab);
            return _scripts[r] = inner == null ? null : sb!.Field(inner, r.PathId)?["m_ClassName"].AsString;
        }

        /// <summary>
        /// The class of a MonoBehaviour by its path id in the bundle; null when it is not there or cannot be told.
        /// </summary>
        public string? ScriptClass(Bundle b, long pid) =>
            b.FileWith(pid) is { } f && f.Assets.file.GetAssetInfo(pid) is { } info ? ScriptClass(b, f, info) : null;

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

    /// <summary>
    /// A bundle open for a scan: its internal files with objects (scene bundles have two), and every
    /// object read once (kept until the bundle is closed).
    /// </summary>
    private sealed class Bundle : IDisposable
    {
        /// <summary>
        /// One internal file of a bundle that has objects.
        /// </summary>
        /// <param name="Cab">The file's name, lower case.</param>
        /// <param name="Assets">The file as AssetsTools opened it.</param>
        public sealed record Inner(string Cab, AssetsFileInstance Assets)
        {
            /// <summary>
            /// A reference's file (m_FileID &gt; 0: one this file lists as needed), by name (lower case).
            /// </summary>
            public string? FileOf(int fileId)
            {
                if (fileId == 0)
                {
                    return Cab;
                }
                var ext = Assets.file.Metadata.Externals;
                return fileId - 1 < ext.Count ? ext[fileId - 1].PathName.Split('/').Last().ToLowerInvariant() : null;
            }
        }

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
        /// The internal files with objects, in the bundle's order.
        /// </summary>
        public List<Inner> Files { get; } = [];

        /// <summary>
        /// Wraps a loaded bundle: its reads go through a <c>BlockCache</c>, and each assets file in it is opened.
        /// </summary>
        public Bundle(AssetsManager am, BundleFileInstance bundle)
        {
            _am = am;
            _bundle = bundle;
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
        /// An object's fields, or null when it is not there or cannot be read.
        /// </summary>
        public AssetTypeValueField? Field(Inner f, long pid) => _fields.TryGetValue((f, pid), out var cached) ? cached : _fields[(f, pid)] = Read(f, pid);

        /// <summary>
        /// An object's fields read and not kept (for the large ones only needed once), or null as Field.
        /// </summary>
        public AssetTypeValueField? Read(Inner f, long pid)
        {
            // Without type trees no object's fields are described (the editor has no class database).
            if (f.Assets.file.Metadata.TypeTreeEnabled && f.Assets.file.GetAssetInfo(pid) is { } info)
            {
                try
                {
                    return _am.GetBaseField(f.Assets, info);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException or NullReferenceException)
                {
                    // A script whose fields are not described: not one of the game's.
                }
            }
            return null;
        }

        /// <summary>
        /// These objects' fields read now, in the order of their files (the bundle is read in blocks: much
        /// faster than one by one in another order); Field then has them.
        /// </summary>
        public void ReadInOrder(IEnumerable<long> pids)
        {
            foreach (var (f, pid) in InOrder(pids))
            {
                Field(f, pid);
            }
        }

        /// <summary>
        /// The same, each given to take and not kept (for large ones of which little is needed); those that
        /// cannot be read left out.
        /// </summary>
        public void ReadInOrder(IEnumerable<long> pids, Action<long, AssetTypeValueField> take)
        {
            foreach (var (f, pid) in InOrder(pids))
            {
                if (Read(f, pid) is { } field)
                {
                    take(pid, field);
                }
            }
        }

        /// <summary>
        /// The objects of the bundle among these path ids, in file and byte order, every other call the other way
        /// round.
        /// </summary>
        private List<(Inner File, long Pid)> InOrder(IEnumerable<long> pids)
        {
            var objects = pids.Distinct().Where(_byId.ContainsKey).Select(p => (File: _byId[p], Pid: p))
                .OrderBy(o => Files.IndexOf(o.File)).ThenBy(o => o.File.Assets.file.GetAssetInfo(o.Pid)!.GetAbsoluteByteOffset(o.File.Assets.file)).ToList();
            // Backwards every other time: what the last read left in the block cache comes first.
            if (_backwards = !_backwards)
            {
                objects.Reverse();
            }
            return objects;
        }

        /// <summary>
        /// Whether the last <c>InOrder</c> went backwards; each call flips it first.
        /// </summary>
        private bool _backwards = true;

        /// <summary>
        /// An object's fields by name (a MonoBehaviour's: its class's), from its file's type tree: nothing of
        /// the object read.
        /// </summary>
        public HashSet<string> FieldNames(Inner f, AssetFileInfo info) =>
            Template(f, info)?.Children.Select(c => c.Name).ToHashSet(StringComparer.Ordinal) ?? [];

        /// <summary>
        /// An object's type tree (a MonoBehaviour's: its class's), or null when it cannot be had.
        /// </summary>
        public AssetTypeTemplateField? Template(Inner f, AssetFileInfo info)
        {
            if (!f.Assets.file.Metadata.TypeTreeEnabled)
            {
                return null;
            }
            try
            {
                return _am.CreateTemplateBaseField(f.Assets, info.TypeId, info.GetScriptIndex(f.Assets.file));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException or NullReferenceException)
            {
                return null;
            }
        }

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
        /// A reference (m_FileID, m_PathID) made in a file: null for none (path id 0).
        /// </summary>
        public static Ref? RefOf(Inner f, AssetTypeValueField pptr)
        {
            long pid = pptr["m_PathID"].AsLong;
            return pid != 0 && f.FileOf(pptr["m_FileID"].AsInt) is string cab ? new Ref(cab, pid) : null;
        }

        /// <summary>
        /// Data kept out of the object, in a resource file of the bundle (.resS): meshes. Empty when the bundle has no
        /// such resource file.
        /// </summary>
        public byte[] StreamBytes(AssetTypeValueField streamData)
        {
            int idx = _bundle.file.GetFileIndex(streamData["path"].AsString.Split('/').Last());
            if (idx < 0)
            {
                return [];
            }
            _bundle.file.GetFileRange(idx, out long start, out _);
            _bundle.file.DataReader.Position = start + streamData["offset"].AsLong;
            return _bundle.file.DataReader.ReadBytes((int)streamData["size"].AsUInt);
        }

        /// <summary>
        /// Unloads the bundle's assets files and the bundle.
        /// </summary>
        public void Dispose()
        {
            foreach (var f in Files)
            {
                _am.UnloadAssetsFile(f.Assets);
            }
            _am.UnloadBundleFile(_bundle);
        }
    }

    /// <summary>
    /// A bundle's data through a larger cache of its blocks than AssetsTools keeps (it keeps 23 blocks of
    /// 128 KiB; this one 512, 64 MiB):
    /// the readers at the editor's start go back and forth between thousands of them in the largest
    /// bundle, each decompressed again when it is read after leaving the cache. The block used longest
    /// ago goes first.
    /// </summary>
    private sealed class BlockCache : Stream
    {
        /// <summary>
        /// A block's size: 128 KiB.
        /// </summary>
        private const int Size = 1 << 17;
        /// <summary>
        /// How many blocks are kept at most.
        /// </summary>
        private const int Most = 512;

        /// <summary>
        /// How much of a bundle it holds.
        /// </summary>
        public const long Bytes = (long)Size * Most;

        /// <summary>
        /// The bundle's own stream, read a block at a time.
        /// </summary>
        private readonly Stream _inner;
        /// <summary>
        /// The blocks kept, by block index (Position / Size).
        /// </summary>
        private readonly Dictionary<long, LinkedListNode<(long Index, byte[] Data)>> _blocks = [];
        /// <summary>
        /// Last used first.
        /// </summary>
        private readonly LinkedList<(long Index, byte[] Data)> _order = new();

        /// <summary>
        /// A cache over a bundle's data stream.
        /// </summary>
        public BlockCache(Stream inner) => _inner = inner;

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => true;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => _inner.Length;

        /// <inheritdoc/>
        public override long Position { get; set; }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int done = 0;
            while (done < count && Position < Length)
            {
                long index = Position / Size;
                byte[] block = Block(index);
                int at = (int)(Position - index * Size);
                int n = Math.Min(count - done, block.Length - at);
                if (n <= 0)
                {
                    break;
                }
                Buffer.BlockCopy(block, at, buffer, offset + done, n);
                done += n;
                Position += n;
            }
            return done;
        }

        /// <summary>
        /// A block's bytes (the last block may be shorter), read from the inner stream when it is not kept.
        /// </summary>
        private byte[] Block(long index)
        {
            // The last block used again (most reads are a few bytes): already first.
            if (_order.First is { } first && first.Value.Index == index)
            {
                return first.Value.Data;
            }
            if (_blocks.TryGetValue(index, out var kept))
            {
                _order.Remove(kept);
                _order.AddFirst(kept);
                return kept.Value.Data;
            }
            int size = (int)Math.Min(Size, Length - index * Size);
            byte[]? block = null;
            if (_blocks.Count >= Most)
            {
                var old = _order.Last!.Value;
                _order.RemoveLast();
                _blocks.Remove(old.Index);
                // Its memory used again (blocks are large objects, which the runtime frees late).
                block = old.Data.Length == size ? old.Data : null;
            }
            block ??= new byte[size];
            _inner.Position = index * Size;
            _inner.ReadExactly(block);
            _blocks[index] = _order.AddFirst((index, block));
            return block;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            _ => Length + offset,
        };

        /// <summary>
        /// Nothing to flush: the stream is read only.
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// Not supported: the stream is read only.
        /// </summary>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// Not supported: the stream is read only.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // ---- Values, as the game data's JSON has them.

    /// <summary>
    /// Unity's class id of a GameObject.
    /// </summary>
    private const int GameObjectType = (int)AssetClassID.GameObject;
    /// <summary>
    /// Unity's class id of a Transform.
    /// </summary>
    private const int TransformType = (int)AssetClassID.Transform;
    /// <summary>
    /// Unity's class id of a MonoBehaviour.
    /// </summary>
    private const int MonoBehaviourType = (int)AssetClassID.MonoBehaviour;

    /// <summary>
    /// The "snappoint" tag (the first custom tag).
    /// </summary>
    private const int SnapTag = 20000;

    /// <summary>
    /// Whether a field is present (AssetsTools gives a dummy for a missing one).
    /// </summary>
    private static bool Has(AssetTypeValueField f, string name) => !f[name].IsDummy;

    /// <summary>
    /// A field's value: ints (the game's bools are bytes: 0 or 1), floats (as doubles), strings; lists
    /// and structures as lists and dictionaries; null when there is none.
    /// </summary>
    private static object? Py(AssetTypeValueField f)
    {
        if (f.IsDummy)
        {
            return null;
        }
        if (f.Value != null && f.Value.ValueType != AssetValueType.Array)
        {
            return f.Value.ValueType switch
            {
                AssetValueType.Bool => f.AsBool,
                AssetValueType.Int8 => (long)f.AsSByte,
                AssetValueType.UInt8 => (long)f.AsByte,
                AssetValueType.Int16 => (long)f.AsShort,
                AssetValueType.UInt16 => (long)f.AsUShort,
                AssetValueType.Int32 => (long)f.AsInt,
                AssetValueType.UInt32 => (long)f.AsUInt,
                AssetValueType.Int64 => f.AsLong,
                AssetValueType.UInt64 => (long)f.AsULong,
                AssetValueType.Float => (double)f.AsFloat,
                AssetValueType.Double => f.AsDouble,
                AssetValueType.String => f.AsString,
                _ => throw new NotSupportedException($"field {f.TemplateField.Name}: {f.Value.ValueType}"),
            };
        }
        if (f.TemplateField.IsArray)
        {
            return f.Children.Select(Py).ToList();
        }
        if (f.Children.Count == 1 && f.Children[0].TemplateField.IsArray)
        {
            return Py(f.Children[0]);
        }
        var d = new CatalogueJson.Dict();
        foreach (var c in f.Children)
        {
            d[c.TemplateField.Name] = Py(c);
        }
        return d;
    }

    /// <summary>
    /// A field's value, or the fallback when there is no such field.
    /// </summary>
    private static object? Get(AssetTypeValueField f, string name, object? fallback) => Has(f, name) ? Py(f[name]) : fallback;

    /// <summary>
    /// A value read by <c>Py</c> as a double (a bool as 0 or 1); throws InvalidDataException for anything else.
    /// </summary>
    private static double Num(object? v) => v switch
    {
        long l => l,
        double d => d,
        bool b => b ? 1 : 0,
        _ => throw new InvalidDataException($"not a number: {v}"),
    };

    /// <summary>
    /// As Python's int(): floats cut toward zero.
    /// </summary>
    private static long Int(object? v) => v switch
    {
        long l => l,
        double d => (long)d,
        bool b => b ? 1 : 0,
        _ => throw new InvalidDataException($"not a number: {v}"),
    };

    /// <summary>
    /// Whether a value is true as Python sees it (0, empty strings and empty collections are not).
    /// </summary>
    private static bool Truthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        long l => l != 0,
        double d => d != 0,
        string s => s.Length > 0,
        System.Collections.ICollection c => c.Count > 0,
        CatalogueJson.Dict d => d.Count > 0,
        _ => true,
    };

    /// <summary>
    /// Rounded to n decimals: floats only (an int stays one).
    /// </summary>
    private static object? Round(object? v, int n) => v is double d ? CatalogueJson.Round(d, n) : v;

    /// <summary>
    /// Rounded half to even at n decimals, as Python's round.
    /// </summary>
    private static double R(double v, int n) => CatalogueJson.Round(v, n);

    /// <summary>
    /// The larger, the first when they are equal (as Python's max: -0.0 before 0 stays -0.0).
    /// </summary>
    private static double Max(double a, double b) => b > a ? b : a;

    /// <summary>
    /// The children of a Transform, as GameObjects' path ids (children that cannot be read left out).
    /// </summary>
    private static IEnumerable<long> ChildObjects(Bundle b, AssetTypeValueField tr)
    {
        foreach (var ch in tr["m_Children.Array"].Children)
        {
            if (b.Field(ch["m_PathID"].AsLong) is { } ct)
            {
                yield return ct["m_GameObject.m_PathID"].AsLong;
            }
        }
    }

    /// <summary>
    /// Whether a MonoBehaviour's fields are a Piece's (m_category, m_resources, m_craftingStation).
    /// </summary>
    private static bool IsPiece(AssetTypeValueField f) => Has(f, "m_category") && Has(f, "m_resources") && Has(f, "m_craftingStation");

    /// <summary>
    /// A Vector3 field as doubles.
    /// </summary>
    private static (double X, double Y, double Z) Vec(AssetTypeValueField v) => (v["x"].AsFloat, v["y"].AsFloat, v["z"].AsFloat);

    /// <summary>
    /// A rotation (x, y, z, w). A prefab root's frame is the identity in whole numbers (Whole): turned
    /// negative, its zeros stay 0.0 where computed ones turn -0.0, and the sign of a zero shows in the
    /// files ("-0.0"); written, its parts are whole numbers ("0", "1").
    /// </summary>
    /// <param name="X">The x part.</param>
    /// <param name="Y">The y part.</param>
    /// <param name="Z">The z part.</param>
    /// <param name="W">The w part.</param>
    /// <param name="Whole">The root's identity in whole numbers, written as such (see the summary).</param>
    private readonly record struct Quat(double X, double Y, double Z, double W, bool Whole = false)
    {
        /// <summary>
        /// The prefab root's frame: the identity, in whole numbers.
        /// </summary>
        public static readonly Quat Root = new(0, 0, 0, 1, true);

        /// <summary>
        /// A Quaternion field's rotation.
        /// </summary>
        public static Quat Of(AssetTypeValueField q) => new(q["x"].AsFloat, q["y"].AsFloat, q["z"].AsFloat, q["w"].AsFloat);

        /// <summary>
        /// Negated: for a whole rotation 0 - v, so a zero stays 0.0.
        /// </summary>
        private double Neg(double v) => Whole ? 0 - v : -v;

        /// <summary>
        /// The product this * b (b applied first).
        /// </summary>
        public Quat Mul(Quat b) => new(
            W * b.X + X * b.W + Y * b.Z - Z * b.Y,
            W * b.Y - X * b.Z + Y * b.W + Z * b.X,
            W * b.Z + X * b.Y - Y * b.X + Z * b.W,
            W * b.W - X * b.X - Y * b.Y - Z * b.Z);

        /// <summary>
        /// v turned by this rotation (q v q^-1).
        /// </summary>
        public (double X, double Y, double Z) Rotate(double vx, double vy, double vz)
        {
            double nx = Neg(X), ny = Neg(Y), nz = Neg(Z);
            double ix = W * vx + Y * vz - Z * vy, iy = W * vy + Z * vx - X * vz, iz = W * vz + X * vy - Y * vx, iw = nx * vx - Y * vy - Z * vz;
            return (ix * W + iw * nx + iy * nz - iz * ny, iy * W + iw * ny + iz * nx - ix * nz, iz * W + iw * nz + ix * ny - iy * nx);
        }

        /// <summary>
        /// v turned by this rotation.
        /// </summary>
        public (double X, double Y, double Z) Rotate((double X, double Y, double Z) v) => Rotate(v.X, v.Y, v.Z);

        /// <summary>
        /// Its parts rounded to n decimals (whole numbers stay whole).
        /// </summary>
        public object[] Rounded(int n) => Whole ? [0L, 0L, 0L, 1L] : [R(X, n), R(Y, n), R(Z, n), R(W, n)];
    }

    /// <summary>
    /// The sum of two vectors.
    /// </summary>
    private static (double X, double Y, double Z) Add((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>
    /// Two vectors multiplied part by part.
    /// </summary>
    private static (double X, double Y, double Z) Scale((double X, double Y, double Z) a, (double X, double Y, double Z) s) => (a.X * s.X, a.Y * s.Y, a.Z * s.Z);

    /// <summary>
    /// Bytes per component, by Unity's VertexFormat (Float, Float16, UNorm8, SNorm8, UNorm16, SNorm16, UInt8, SInt8,
    /// UInt16, SInt16, UInt32, SInt32).
    /// </summary>
    private static readonly int[] VertexFormatSize = [4, 2, 1, 1, 2, 2, 1, 1, 2, 2, 4, 4];

    /// <summary>
    /// A mesh's vertices (x, y, z each) and each submesh's triangles, as UnityPy reads them (the
    /// submesh's base vertex not added); null when it has no vertex data, or one not read here.
    /// </summary>
    private static (float[] Vertices, List<int[]> Triangles)? MeshData(Bundle b, AssetTypeValueField m)
    {
        var vd = m["m_VertexData"];
        int n = (int)vd["m_VertexCount"].AsUInt;
        byte[] data = vd["m_DataSize"].AsByteArray;
        if (Has(m, "m_StreamData") && m["m_StreamData.path"].AsString.Length > 0)
        {
            data = b.StreamBytes(m["m_StreamData"]);
        }
        if (n == 0 || data.Length == 0)
        {
            return null;
        }
        var channels = vd["m_Channels.Array"].Children.Select(c => (Stream: (int)c["stream"].AsByte, Offset: (int)c["offset"].AsByte, Format: (int)c["format"].AsByte, Dim: c["dimension"].AsByte & 0xF)).ToList();
        if (channels.Count == 0 || channels[0].Dim < 3 || channels[0].Format > 1 || channels.Any(c => c.Dim > 0 && c.Format >= VertexFormatSize.Length))
        {
            return null;
        }
        // Streams one after another, each 16-byte aligned; in a stream a vertex's channels side by side.
        int streams = channels.Max(c => c.Stream) + 1;
        int[] stride = new int[streams];
        int[] start = new int[streams];
        foreach (var c in channels.Where(c => c.Dim > 0))
        {
            stride[c.Stream] += c.Dim * VertexFormatSize[c.Format];
        }
        for (int s = 1; s < streams; s++)
        {
            start[s] = (start[s - 1] + n * stride[s - 1] + 15) & ~15;
        }
        var pos = channels[0];
        float[] vertices = new float[n * 3];
        for (int v = 0; v < n; v++)
        {
            int p = start[pos.Stream] + v * stride[pos.Stream] + pos.Offset;
            for (int d = 0; d < 3; d++)
            {
                vertices[v * 3 + d] = pos.Format == 0 ? BitConverter.ToSingle(data, p + 4 * d) : (float)BitConverter.ToHalf(data, p + 2 * d);
            }
        }
        byte[] ib = m["m_IndexBuffer.Array"].AsByteArray;
        bool wide = m["m_IndexFormat"].AsInt == 1;
        int count = ib.Length / (wide ? 4 : 2);
        int[] indices = new int[count];
        for (int k = 0; k < count; k++)
        {
            indices[k] = wide ? (int)BitConverter.ToUInt32(ib, 4 * k) : BitConverter.ToUInt16(ib, 2 * k);
        }
        var triangles = new List<int[]>();
        foreach (var sm in m["m_SubMeshes.Array"].Children)
        {
            // Triangles only (topology 0): the game's colliders and rocks have nothing else.
            if (sm["topology"].AsInt != 0)
            {
                return null;
            }
            int first = (int)sm["firstByte"].AsUInt / (wide ? 4 : 2), end = Math.Min(first + (int)sm["indexCount"].AsUInt, count);
            triangles.Add(first < end ? indices[first..end] : []);
        }
        return (vertices, triangles);
    }
}
