namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    internal static partial class Localization
    {
        /// <summary>
        /// The row splitter behind <see cref="Csv"/>: fed one character at a time, it hands back each row as soon as
        /// the character that completes it arrives. Its per-state character rules are in the other part of this class.
        /// </summary>
        internal sealed partial class CsvScanner
        {
            /// <summary>
            /// The state at the start of a field.
            /// </summary>
            private const int StartOfField = 0;

            /// <summary>
            /// The state inside an unquoted field.
            /// </summary>
            private const int InField = 1;

            /// <summary>
            /// The state inside a quoted field.
            /// </summary>
            private const int InQuotes = 2;

            /// <summary>
            /// The state after a quote inside a quoted field: a second quote is a literal quote, a comma or a line break ends the field.
            /// </summary>
            private const int QuoteInQuotes = 3;

            /// <summary>
            /// The state after a line break, until the next character starts a new row or a lone carriage return is skipped.
            /// </summary>
            private const int AfterLineBreak = 4;

            /// <summary>
            /// The fields of the row being read.
            /// </summary>
            private List<string> _row = [];

            /// <summary>
            /// The text of the field being read.
            /// </summary>
            private readonly System.Text.StringBuilder _field = new();

            /// <summary>
            /// The current state, one of the state constants.
            /// </summary>
            private int _state;

            /// <summary>
            /// Whether the current row has had any character (so that an empty line is a row of no fields).
            /// </summary>
            private bool _rowStarted;

            /// <summary>
            /// Reads one character. Returns the row it completes, or null.
            /// </summary>
            /// <param name="c">The next character of the text.</param>
            public List<string>? Feed(char c)
            {
                List<string>? done = null;
                if (_state == AfterLineBreak && c is not '\n' and not '\r')
                {
                    // Text after a lone carriage return: the next row, this character its first.
                    done = TakeRow();
                }
                Step(c);
                return done;
            }

            /// <summary>
            /// Ends a line. Returns the row it completes, or null when a quoted field goes on to the next line.
            /// </summary>
            public List<string>? EndLine()
            {
                switch (_state)
                {
                    case InField or QuoteInQuotes:
                        AddField();
                        break;
                    case StartOfField when _rowStarted:
                        _row.Add("");
                        break;
                    default:
                        break;
                }
                return _state == InQuotes ? null : TakeRow();
            }

            /// <summary>
            /// Ends the text. Returns the unfinished row when a quoted field was never closed, else null.
            /// </summary>
            public List<string>? EndText()
            {
                if (_state != InQuotes)
                {
                    return null;
                }
                AddField();
                return _row;
            }

            /// <summary>
            /// Gives the character to the rule of the current state.
            /// </summary>
            /// <param name="c">The character being read.</param>
            private void Step(char c)
            {
                switch (_state)
                {
                    case StartOfField:
                        OnStartOfField(c);
                        break;
                    case InField:
                        OnInField(c);
                        break;
                    case InQuotes:
                        OnInQuotes(c);
                        break;
                    case QuoteInQuotes:
                        OnQuoteInQuotes(c);
                        break;
                    case AfterLineBreak:
                        // The line feed of a CRLF pair, or a carriage return on its own: still after the break.
                        break;
                    default:
                        throw new InvalidOperationException($"the CSV scanner is in no state ({_state})");
                }
            }

            /// <summary>
            /// Closes the current field into the row.
            /// </summary>
            private void AddField()
            {
                _row.Add(_field.ToString());
                _field.Clear();
            }

            /// <summary>
            /// Returns the finished row and starts an empty one.
            /// </summary>
            private List<string> TakeRow()
            {
                List<string> done = _row;
                _row = [];
                _state = StartOfField;
                _rowStarted = false;
                return done;
            }
        }
    }
}
