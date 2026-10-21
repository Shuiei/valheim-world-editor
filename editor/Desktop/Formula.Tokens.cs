using System.Globalization;
using System.Text.RegularExpressions;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The formula's tokens: the numbers, names and operators it is written with.
/// </summary>
public static partial class Formula
{
    /// <summary>
    /// A token of the formula: a number, a name or an operator (only one is set).
    /// </summary>
    /// <param name="N">A number.</param>
    /// <param name="Id">A name (variable, function, pi).</param>
    /// <param name="Op">An operator or bracket.</param>
    internal sealed record Tok(double? N = null, string? Id = null, string? Op = null)
    {
        /// <summary>
        /// The token as it is written, for messages.
        /// </summary>
        public override string ToString() => Op ?? Id ?? N?.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>
    /// One token from where it is asked for (\G), spaces before it skipped: a number, a name, or an operator.
    /// </summary>
    private static readonly Regex Token = new(@"\G\s*(?:(\d+\.?\d*(?:[eE][+-]?\d+)?|\.\d+(?:[eE][+-]?\d+)?)|([A-Za-z_]\w*)|(<=|>=|==|!=|&&|\|\||[-+*/%^(),<>?:!]))", RegexOptions.Compiled);

    /// <summary>
    /// Splits the formula into its tokens, in order; a piece that is none of them is a mistake in the formula.
    /// </summary>
    /// <param name="src">The formula, trimmed.</param>
    /// <returns>The tokens (none for an empty formula).</returns>
    internal static List<Tok> Tokenize(string src)
    {
        var toks = new List<Tok>();
        int pos = 0;
        while (pos < src.Length)
        {
            var m = Token.Match(src, pos);
            if (!m.Success || m.Length == 0)
            {
                string rest = src[pos..].Trim();
                throw new FormatException(Lang.T("shape.error.notUnderstood", rest[..Math.Min(12, rest.Length)]));
            }
            toks.Add(m.Groups[1].Success ? new Tok(N: double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                : m.Groups[2].Success ? new Tok(Id: m.Groups[2].Value) : new Tok(Op: m.Groups[3].Value));
            pos = m.Index + m.Length;
        }
        return toks;
    }
}
