using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// Objects read as laid out, a few numbers each rather than their whole fields: for the readers at
    /// the editor's start. The bundle is read in blocks, so they are read in the order of their file.
    /// </summary>
    internal static partial class Layout
    {
        /// <summary>
        /// An object's first reference (a component's m_GameObject): its file id and path id.
        /// </summary>
        public static (int FileId, long PathId) First(Bundle.Inner f, AssetFileInfo info)
        {
            var r = At(f, info);
            return (r.ReadInt32(), r.ReadInt64());
        }

        /// <summary>
        /// The prefab roots among these GameObjects (each in its file): those whose Transform has no
        /// parent. A GameObject has its components first (a count, then each a file id and a path id); a
        /// Transform has its parent after its GameObject, rotation, position, scale and children.
        /// </summary>
        public static HashSet<long> Roots(IEnumerable<(Bundle.Inner File, long Go)> gos)
        {
            var transforms = new List<(Bundle.Inner, AssetFileInfo?, long)>();
            foreach (var (f, info, go) in InOrder(gos.Distinct().Select(g => (g.File, (AssetFileInfo?)g.File.Assets.file.GetAssetInfo(g.Go), g.Go))))
            {
                var r = At(f, info);
                for (int i = r.ReadInt32(); i > 0; i--)
                {
                    int file = r.ReadInt32();
                    long pid = r.ReadInt64();
                    if (file == 0 && f.Assets.file.GetAssetInfo(pid) is { TypeId: TransformType } t)
                    {
                        transforms.Add((f, t, go));
                        break;
                    }
                }
            }
            var roots = new HashSet<long>();
            foreach (var (f, info, go) in InOrder(transforms))
            {
                var r = At(f, info);
                r.Position += 12 + 40;
                int children = r.ReadInt32();
                r.Position += 12L * children;
                if (r.ReadInt32() == 0 && r.ReadInt64() == 0)
                {
                    roots.Add(go);
                }
            }
            return roots;
        }

        /// <summary>
        /// By file, then by place in it (those that are not there left out); with what they stand for.
        /// </summary>
        private static IEnumerable<(Bundle.Inner File, AssetFileInfo Info, long Of)> InOrder(IEnumerable<(Bundle.Inner File, AssetFileInfo? Info, long Of)> objects) =>
            objects.Where(o => o.Info != null).Select(o => (o.File, Info: o.Info!, o.Of))
                .OrderBy(o => o.File.Cab, StringComparer.Ordinal).ThenBy(o => o.Info.GetAbsoluteByteOffset(o.File.Assets.file));

        /// <summary>
        /// The file's reader, at the start of the object's bytes.
        /// </summary>
        private static AssetsFileReader At(Bundle.Inner f, AssetFileInfo info)
        {
            var file = f.Assets.file;
            file.Reader.Position = info.GetAbsoluteByteOffset(file);
            return file.Reader;
        }
    }
}
