using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The formula's shape: the tokens read in order into a function of the environment, as a recursive descent parser.
/// </summary>
public static partial class Formula
{
    /// <summary>
    /// Reads the tokens of one formula; each rule takes the tokens its level of operators owns.
    /// </summary>
    internal sealed partial class Parser
    {
        /// <summary>
        /// The formula's tokens, in order.
        /// </summary>
        private readonly List<Tok> _toks;
        /// <summary>
        /// The variables the formula may use.
        /// </summary>
        private readonly IReadOnlyList<string> _names;
        /// <summary>
        /// The next token to read.
        /// </summary>
        private int _i;
        /// <summary>
        /// How deep the rules are nested now (see MaxDepth).
        /// </summary>
        private int _depth;

        /// <summary>
        /// A parser over the tokens of one formula.
        /// </summary>
        /// <param name="toks">The formula's tokens, in order.</param>
        /// <param name="names">The variables the formula may use.</param>
        public Parser(List<Tok> toks, IReadOnlyList<string> names)
        {
            _toks = toks;
            _names = names;
        }

        /// <summary>
        /// Reads the whole formula: every token must be used.
        /// </summary>
        /// <returns>The formula's function.</returns>
        public Func<Env, double> Parse()
        {
            var result = Ternary();
            if (_i < _toks.Count)
            {
                throw new FormatException(Lang.T("shape.error.outOfPlace", _toks[_i].ToString()));
            }
            return result;
        }

        /// <summary>
        /// The next token, or null at the end.
        /// </summary>
        internal Tok? Peek() => _i < _toks.Count ? _toks[_i] : null;

        /// <summary>
        /// Reads the next token when it is this operator or bracket.
        /// </summary>
        /// <returns>Whether it was read.</returns>
        internal bool Eat(string op)
        {
            if (Peek()?.Op == op)
            {
                _i++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Reads this operator or bracket, or fails with a message naming what came instead.
        /// </summary>
        internal void Need(string op)
        {
            if (!Eat(op))
            {
                throw new FormatException(Peek() is { } t ? Lang.T("shape.error.expectedBefore", op, t.ToString()) : Lang.T("shape.error.expectedAtEnd", op));
            }
        }
    }
}
