using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// Reading a bundle's objects: their fields, read once and in the bundle's block order, and the objects found by path id.
/// </summary>
public static partial class GameCatalogues
{
    internal sealed partial class Bundle
    {
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
                    Log.CaughtBrief(e, "reading a script's fields (not described: not one of the game's)");
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
        /// A reference (m_FileID, m_PathID) made in a file: null for none (path id 0).
        /// </summary>
        public static Ref? RefOf(Inner f, AssetTypeValueField pptr)
        {
            long pid = pptr["m_PathID"].AsLong;
            return pid != 0 && f.FileOf(pptr["m_FileID"].AsInt) is string cab ? new Ref(cab, pid) : null;
        }
    }
}
