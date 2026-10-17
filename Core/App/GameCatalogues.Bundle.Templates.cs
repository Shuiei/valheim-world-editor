using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// An object's type tree in a bundle: the names of its fields, and the tree itself.
/// </summary>
public static partial class GameCatalogues
{
    internal sealed partial class Bundle
    {
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
                Log.CaughtBrief(e, "making an object's type tree (none: its fields are not read)");
                return null;
            }
        }
    }
}
