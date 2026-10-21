// The English names and texts of the game's localisation, read from its text assets (see GameCatalogues).

using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The game's English texts (its localization tables, in resources.assets): token -&gt; text.
    /// </summary>
    internal static partial class Localization
    {
        /// <summary>
        /// Unity's class id of a TextAsset.
        /// </summary>
        private const int TextAssetType = (int)AssetClassID.TextAsset;

        /// <summary>
        /// The English texts of the localisation tables in resources.assets (three folders up from the bundles); the
        /// first text of a token wins.
        /// </summary>
        public static Dictionary<string, string> English(string bundlesDir)
        {
            string data = Path.GetFullPath(Path.Combine(bundlesDir, "..", "..", ".."));
            var am = new AssetsManager();
            try
            {
                var a = am.LoadAssetsFile(Path.Combine(data, "resources.assets"), false);
                var english = new Dictionary<string, string>();
                foreach (var info in a.file.AssetInfos.Where(i => i.TypeId == TextAssetType))
                {
                    var (name, script) = TextAsset(am, a, info);
                    if (!name.StartsWith("localization", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    foreach (var row in Csv(script.TrimStart('﻿')))
                    {
                        if (row.Count > 1 && row[0].Length > 0 && row[1].Length > 0)
                        {
                            english.TryAdd(Strip(row[0]), Strip(row[1]));
                        }
                    }
                }
                return english;
            }
            finally
            {
                am.UnloadAll();
            }
        }

        /// <summary>
        /// A TextAsset's name and text: by its fields when the file describes them, else read as laid out
        /// (a name, then the text, each a length and UTF-8 bytes, 4-byte aligned).
        /// </summary>
        private static (string Name, string Script) TextAsset(AssetsManager am, AssetsFileInstance a, AssetFileInfo info)
        {
            if (a.file.Metadata.TypeTreeEnabled)
            {
                var f = am.GetBaseField(a, info);
                return (f["m_Name"].AsString, f["m_Script"].AsString);
            }
            var r = a.file.Reader;
            long start = info.GetAbsoluteByteOffset(a.file);
            r.Position = start;
            string Next()
            {
                int n = r.ReadInt32();
                string s = System.Text.Encoding.UTF8.GetString(r.ReadBytes(n));
                r.Position = start + ((r.Position - start + 3) & ~3L);
                return s;
            }
            string name = Next();
            return (name, Next());
        }

        /// <summary>
        /// Whitespace off both ends, as Python's str.strip (which also takes the separators \x1c-\x1f).
        /// </summary>
        public static string Strip(string s)
        {
            static bool Space(char c) => char.IsWhiteSpace(c) || c is >= '\x1c' and <= '\x1f';
            int a = 0, b = s.Length;
            while (a < b && Space(s[a]))
            {
                a++;
            }
            while (b > a && Space(s[b - 1]))
            {
                b--;
            }
            return s[a..b];
        }
    }
}
