using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        /// <summary>
        /// Fields of an object read raw, as its type tree lays them out: each field after the other, a
        /// list or text as its count then its items, padded to 4 bytes after those marked so.
        /// </summary>
        private static partial class Raw
        {
            /// <summary>
            /// The byte size of a number type, 0 for others.
            /// </summary>
            private static int Size(AssetValueType t) => t switch
            {
                AssetValueType.Bool or AssetValueType.Int8 or AssetValueType.UInt8 => 1,
                AssetValueType.Int16 or AssetValueType.UInt16 => 2,
                AssetValueType.Int32 or AssetValueType.UInt32 or AssetValueType.Float => 4,
                AssetValueType.Int64 or AssetValueType.UInt64 or AssetValueType.Double => 8,
                _ => 0,
            };

            /// <summary>
            /// Whether a field can be stepped over raw: numbers, texts, byte arrays, lists of such, and structures of
            /// such.
            /// </summary>
            public static bool CanSkip(AssetTypeTemplateField t) =>
                t.IsArray ? t.Children.Count == 2 && CanSkip(t.Children[1])
                : t.ValueType is AssetValueType.String or AssetValueType.ByteArray || Size(t.ValueType) > 0 || t.ValueType == AssetValueType.None && t.Children.Count > 0 && t.Children.All(CanSkip);

            /// <summary>
            /// A number, a yes/no, a text, or a reference (its path id).
            /// </summary>
            public static bool CanRead(AssetTypeTemplateField t) =>
                t.ValueType is AssetValueType.Bool or AssetValueType.Int8 or AssetValueType.UInt8 or AssetValueType.Int32 or AssetValueType.Float or AssetValueType.String
                || t.ValueType == AssetValueType.None && t.Children.Count == 2 && t.Children[0].Name == "m_FileID" && t.Children[1].Name == "m_PathID";
        }
    }
}
