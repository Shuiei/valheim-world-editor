namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    internal static partial class Localization
    {
        internal sealed partial class CsvScanner
        {
            /// <summary>
            /// A character at the start of a field: a line break ends an empty row or an empty field, a quote opens quotes,
            /// a comma ends an empty field, anything else starts an unquoted field.
            /// </summary>
            /// <param name="c">The character being read.</param>
            private void OnStartOfField(char c)
            {
                if (!_rowStarted && c is '\n' or '\r')
                {
                    _state = AfterLineBreak;
                    return;
                }
                _rowStarted = true;
                if (c is '\n' or '\r')
                {
                    _row.Add("");
                    _state = AfterLineBreak;
                }
                else if (c == '"')
                {
                    _state = InQuotes;
                }
                else if (c == ',')
                {
                    _row.Add("");
                }
                else
                {
                    _field.Append(c);
                    _state = InField;
                }
            }

            /// <summary>
            /// A character in an unquoted field: a comma or a line break closes the field, anything else is kept.
            /// </summary>
            /// <param name="c">The character being read.</param>
            private void OnInField(char c)
            {
                if (c is '\n' or '\r')
                {
                    AddField();
                    _state = AfterLineBreak;
                }
                else if (c == ',')
                {
                    AddField();
                    _state = StartOfField;
                }
                else
                {
                    _field.Append(c);
                }
            }

            /// <summary>
            /// A character in a quoted field: a quote may close the quotes, anything else is kept.
            /// </summary>
            /// <param name="c">The character being read.</param>
            private void OnInQuotes(char c)
            {
                if (c == '"')
                {
                    _state = QuoteInQuotes;
                }
                else
                {
                    _field.Append(c);
                }
            }

            /// <summary>
            /// A character after a quote inside quotes: a second quote is a literal quote, a comma or a line break ends
            /// the field, anything else goes on the field unquoted.
            /// </summary>
            /// <param name="c">The character being read.</param>
            private void OnQuoteInQuotes(char c)
            {
                if (c == '"')
                {
                    _field.Append('"');
                    _state = InQuotes;
                }
                else if (c == ',')
                {
                    AddField();
                    _state = StartOfField;
                }
                else if (c is '\n' or '\r')
                {
                    AddField();
                    _state = AfterLineBreak;
                }
                else
                {
                    _field.Append(c);
                    _state = InField;
                }
            }
        }
    }
}
