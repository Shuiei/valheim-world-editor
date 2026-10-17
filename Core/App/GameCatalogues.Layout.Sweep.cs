namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    internal static partial class Layout
    {
        /// <summary>
        /// What <c>Sweep</c> gives each object read raw: its file, path id, Unity class id and bytes.
        /// </summary>
        public delegate void Parse(Bundle.Inner f, long pid, int type, Bytes data);

        /// <summary>
        /// These objects (each in its file) read raw in one sweep of the bundle, in the order of its files,
        /// each given whole to parse; with: others read whole (Field) in the same sweep.
        /// </summary>
        public static void Sweep(Bundle b, IEnumerable<(Bundle.Inner File, long Pid)> raw, Parse parse, IEnumerable<(Bundle.Inner File, long Pid)>? with = null)
        {
            var rawIn = raw.ToLookup(o => o.File, o => o.Pid);
            var wholeIn = (with ?? []).ToLookup(o => o.File, o => o.Pid);
            foreach (var f in b.Files)
            {
                var file = f.Assets.file;
                // Where each starts; read whole: -1 as its size.
                var objects = rawIn[f].Distinct().Select(p => (Info: file.GetAssetInfo(p), Whole: false)).Concat(wholeIn[f].Distinct().Select(p => (Info: file.GetAssetInfo(p), Whole: true)))
                    .Where(o => o.Info != null).Select(o => (o.Info!.PathId, At: o.Info.GetAbsoluteByteOffset(file), Size: o.Whole ? -1 : (int)o.Info.ByteSize, o.Info.TypeId)).ToArray();
                Array.Sort(objects, (x, y) => x.At.CompareTo(y.At));
                foreach (var (pid, at, size, type) in objects)
                {
                    if (size < 0)
                    {
                        b.Field(f, pid);
                        continue;
                    }
                    file.Reader.Position = at;
                    parse(f, pid, type, new Bytes(file.Reader.ReadBytes(size), file.Reader.BigEndian));
                }
            }
        }
    }
}
