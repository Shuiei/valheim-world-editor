// The English names and texts of the game's localisation, read from its text assets (see GameCatalogues).

using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The game's English texts (its localization tables, in resources.assets): token -&gt; text.
    /// </summary>
    private static class Localization
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

        /// <summary>
        /// Rows of comma-separated text as Python's csv module reads them (quoted fields may hold commas,
        /// line breaks and doubled quotes; a quote inside an unquoted field is kept as it is). A lone carriage return
        /// followed by more text on the line ends the row, as a line break does (Python's csv refuses it, and one odd
        /// line of a localisation table then stopped every world opening).
        /// </summary>
        public static IEnumerable<List<string>> Csv(string text)
        {
            var row = new List<string>();
            var field = new System.Text.StringBuilder();
            int state = 0; // 0 start of a field, 1 in a field, 2 in quotes, 3 a quote in quotes, 4 after a line break
            bool rowStarted = false;
            foreach (string line in Lines(text))
            {
                for (int k = 0; k < line.Length; k++)
                {
                    char c = line[k];
                    if (state == 4 && c is not '\n' and not '\r')
                    {
                        // Text after a lone carriage return: the next row, this character its first.
                        yield return row;
                        row = [];
                        state = 0;
                        rowStarted = false;
                    }
                    switch (state)
                    {
                        case 0 when !rowStarted && (c == '\n' || c == '\r'):
                            state = 4;
                            break;
                        case 0:
                            rowStarted = true;
                            if (c is '\n' or '\r')
                            {
                                row.Add("");
                                state = 4;
                            }
                            else if (c == '"')
                            {
                                state = 2;
                            }
                            else if (c == ',')
                            {
                                row.Add("");
                            }
                            else
                            {
                                field.Append(c);
                                state = 1;
                            }
                            break;
                        case 1:
                            if (c is '\n' or '\r')
                            {
                                row.Add(field.ToString());
                                field.Clear();
                                state = 4;
                            }
                            else if (c == ',')
                            {
                                row.Add(field.ToString());
                                field.Clear();
                                state = 0;
                            }
                            else
                            {
                                field.Append(c);
                            }
                            break;
                        case 2:
                            if (c == '"')
                            {
                                state = 3;
                            }
                            else
                            {
                                field.Append(c);
                            }
                            break;
                        case 3:
                            if (c == '"')
                            {
                                field.Append('"');
                                state = 2;
                            }
                            else if (c == ',')
                            {
                                row.Add(field.ToString());
                                field.Clear();
                                state = 0;
                            }
                            else if (c is '\n' or '\r')
                            {
                                row.Add(field.ToString());
                                field.Clear();
                                state = 4;
                            }
                            else
                            {
                                field.Append(c);
                                state = 1;
                            }
                            break;
                        case 4:
                            break;
                        default:
                            break;
                    }
                }
                // The end of a line: the row ends unless a quoted field goes on.
                switch (state)
                {
                    case 1 or 3:
                        row.Add(field.ToString());
                        field.Clear();
                        break;
                    case 0 when rowStarted:
                        row.Add("");
                        break;
                    default:
                        break;
                }
                if (state != 2)
                {
                    yield return row;
                    row = [];
                    state = 0;
                    rowStarted = false;
                }
            }
            // Quotes not closed at the end: the row as it is.
            if (state == 2)
            {
                row.Add(field.ToString());
                yield return row;
            }
        }

        /// <summary>
        /// Lines with their line breaks, split at \n only (as Python's io.StringIO does).
        /// </summary>
        private static IEnumerable<string> Lines(string text)
        {
            int start = 0;
            while (start < text.Length)
            {
                int nl = text.IndexOf('\n', start);
                int end = nl < 0 ? text.Length : nl + 1;
                yield return text[start..end];
                start = end;
            }
        }
    }
}
