using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The formula's primaries as parser rules: a number, a name or call, a bracket, or a sign before a primary.
/// </summary>
public static partial class Formula
{
    /// <summary>
    /// The formula's primaries as parser rules.
    /// </summary>
    private sealed partial class Parser
    {
        /// <summary>
        /// A primary (a number, a name, a call, a bracket or a unary sign), counting how deep the formula nests.
        /// </summary>
        private Func<Env, double> Primary()
        {
            if (++_depth > MaxDepth)
            {
                throw new FormatException(Lang.T("shape.error.nestedTooDeeply"));
            }
            try
            {
                return PrimaryAt();
            }
            finally
            {
                _depth--;
            }
        }

        /// <summary>
        /// Reads one primary from the next token.
        /// </summary>
        private Func<Env, double> PrimaryAt()
        {
            if (_i >= _toks.Count)
            {
                throw new FormatException(Lang.T("shape.error.endsTooEarly"));
            }
            var t = _toks[_i++];
            if (t.N is double n)
            {
                return _ => n;
            }
            if (t.Op == "(")
            {
                var e = Ternary();
                Need(")");
                return e;
            }
            if (t.Op == "-")
            {
                var e = Power();
                return env => -e(env);
            }
            if (t.Op == "!")
            {
                var e = Power();
                return env => Truthy(e(env)) ? 0 : 1;
            }
            if (t.Id is string id)
            {
                return Name(id);
            }
            throw new FormatException(Lang.T("shape.error.outOfPlace", t.Op));
        }
    }
}
