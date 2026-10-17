namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    internal static partial class Localization
    {
        /// <summary>
        /// Rows of comma-separated text as Python's csv module reads them (quoted fields may hold commas,
        /// line breaks and doubled quotes; a quote inside an unquoted field is kept as it is). A lone carriage return
        /// followed by more text on the line ends the row, as a line break does (Python's csv refuses it, and one odd
        /// line of a localisation table then stopped every world opening).
        /// </summary>
        public static IEnumerable<List<string>> Csv(string text)
        {
            var scanner = new CsvScanner();
            foreach (string line in Lines(text))
            {
                for (int k = 0; k < line.Length; k++)
                {
                    List<string>? done = scanner.Feed(line[k]);
                    if (done is not null)
                    {
                        yield return done;
                    }
                }
                // The end of a line: the row ends unless a quoted field goes on.
                List<string>? ended = scanner.EndLine();
                if (ended is not null)
                {
                    yield return ended;
                }
            }
            // Quotes not closed at the end: the row as it is.
            List<string>? last = scanner.EndText();
            if (last is not null)
            {
                yield return last;
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
