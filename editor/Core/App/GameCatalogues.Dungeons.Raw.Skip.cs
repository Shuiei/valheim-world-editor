using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        private static partial class Raw
        {
            /// <summary>
            /// Moves <c>at</c> past one field, with its padding.
            /// </summary>
            private static void Skip(AssetTypeTemplateField t, Layout.Bytes d, ref int at)
            {
                if (t.IsArray)
                {
                    int n = d.I32(at);
                    at += 4;
                    for (int i = 0; i < n; i++)
                    {
                        Skip(t.Children[1], d, ref at);
                    }
                }
                else if (t.ValueType is AssetValueType.String or AssetValueType.ByteArray)
                {
                    at += 4 + d.I32(at);
                    if (t.Children.Count > 0 && t.Children[0].IsAligned)
                    {
                        at = (at + 3) & ~3;
                    }
                }
                else if (Size(t.ValueType) is int size and > 0)
                {
                    at += size;
                }
                else
                {
                    foreach (var c in t.Children)
                    {
                        Skip(c, d, ref at);
                    }
                }
                if (t.IsAligned)
                {
                    at = (at + 3) & ~3;
                }
            }

            /// <summary>
            /// The named fields up to the last-th, as Py has them (a reference: its path id).
            /// </summary>
            public static Dictionary<string, object?> Fields(AssetTypeTemplateField tree, int last, string[] names, Layout.Bytes d)
            {
                var res = new Dictionary<string, object?>();
                int at = 0;
                for (int i = 0; i <= last; i++)
                {
                    var c = tree.Children[i];
                    if (names.Contains(c.Name))
                    {
                        res[c.Name] = c.ValueType switch
                        {
                            AssetValueType.Bool => d.U8(at) != 0,
                            AssetValueType.Int8 => (long)(sbyte)d.U8(at),
                            AssetValueType.UInt8 => (long)d.U8(at),
                            AssetValueType.Int32 => (long)d.I32(at),
                            AssetValueType.Float => (double)d.F32(at),
                            AssetValueType.String => d.Text(at),
                            _ => d.I64(at + 4),
                        };
                    }
                    Skip(c, d, ref at);
                }
                return res;
            }
        }
    }
}
