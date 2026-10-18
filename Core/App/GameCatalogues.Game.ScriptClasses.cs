using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// The scripts of the game's bundles: the class of each MonoBehaviour, from the script its file lists.
/// </summary>
public static partial class GameCatalogues
{
    internal sealed partial class Game
    {
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
    }
}
