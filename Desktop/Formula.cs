using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// A small, safe formula language, the same as the web editor's (editor/shapes.js compile): numbers, named variables, +
/// - * / % ^, comparisons, &amp;&amp; ||, !, a ? b : c, pi, n(x, z) (smooth noise) and the functions below. Compiled
/// once into a function of an environment; a mistake throws a FormatException with a plain message, in the user's language (the
/// Shape panel shows it).
/// </summary>
public static partial class Formula
{
    /// <summary>
    /// What a compiled formula reads: its variables' values and the noise behind n(x, z). One is reused from point to
    /// point.
    /// </summary>
    public sealed class Env
    {
        /// <summary>
        /// The variables' values by name (those given to Compile); a missing one throws when the formula runs.
        /// </summary>
        public Dictionary<string, double> Vars { get; } = [];
        /// <summary>
        /// The smooth noise of n(x, z) (0 everywhere unless set).
        /// </summary>
        public Func<double, double, double> Noise { get; set; } = (_, _) => 0;
    }

    /// <summary>
    /// JavaScript's truth: 0 and NaN are false.
    /// </summary>
    private static bool Truthy(double v) => v != 0 && !double.IsNaN(v);

    /// <summary>
    /// smooth(t): 0 to 1 eased (smoothstep), t clamped to 0..1.
    /// </summary>
    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// The functions a formula can call, by name, as in JavaScript's Math where they share a name; sqrt and log take no
    /// negative numbers (they clamp), bell(t) is a bell curve exp(-2.5 t²).
    /// </summary>
    private static readonly Dictionary<string, Func<double[], double>> Funcs = new()
    {
        ["sin"] = a => Math.Sin(a[0]),
        ["cos"] = a => Math.Cos(a[0]),
        ["tan"] = a => Math.Tan(a[0]),
        ["abs"] = a => Math.Abs(a[0]),
        ["sqrt"] = a => Math.Sqrt(Math.Max(0, a[0])),
        ["exp"] = a => Math.Exp(a[0]),
        ["log"] = a => Math.Log(Math.Max(1e-9, a[0])),
        ["floor"] = a => Math.Floor(a[0]),
        ["ceil"] = a => Math.Ceiling(a[0]),
        // JavaScript's Math.round: halves go up.
        ["round"] = a => Math.Floor(a[0] + 0.5),
        ["min"] = a => a.Length == 0 ? double.PositiveInfinity : a.Min(),
        ["max"] = a => a.Length == 0 ? double.NegativeInfinity : a.Max(),
        ["pow"] = a => Math.Pow(a[0], a[1]),
        ["atan2"] = a => Math.Atan2(a[0], a[1]),
        // JavaScript's Math.sign: NaN stays NaN (Math.Sign throws on it).
        ["sign"] = a => double.IsNaN(a[0]) ? double.NaN : Math.Sign(a[0]),
        ["clamp"] = a => Math.Max(a[1], Math.Min(a[2], a[0])),
        ["smooth"] = a => Smooth(a[0]),
        ["bell"] = a => Math.Exp(-a[0] * a[0] * 2.5),
    };

    /// <summary>
    /// How many numbers each function takes when not one (-1, min and max: any number, none included, as in
    /// JavaScript).
    /// </summary>
    private static readonly Dictionary<string, int> Arity = new()
    {
        ["pow"] = 2,
        ["atan2"] = 2,
        ["clamp"] = 3,
        ["min"] = -1,
        ["max"] = -1,
    };

    /// <summary>
    /// Deeper nesting, or a longer formula, would run out of stack (which closes the app: it cannot be caught) when it
    /// is read or worked out.
    /// </summary>
    private const int MaxDepth = 100, MaxTokens = 4000;

    /// <summary>
    /// Applies a function to its arguments' values. The numbers go in one array kept for the next point; a second
    /// thread running the formula at the same time meanwhile gets its own.
    /// </summary>
    /// <param name="f">The function, given the argument values.</param>
    /// <param name="arr">The arguments.</param>
    internal static Func<Env, double> Apply(Func<double[], double> f, Func<Env, double>[] arr)
    {
        double[]? spare = new double[arr.Length];
        return env =>
        {
            double[] values = Interlocked.Exchange(ref spare, null) ?? new double[arr.Length];
            try
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    values[i] = arr[i](env);
                }
                return f(values);
            }
            finally
            {
                Volatile.Write(ref spare, values);
            }
        };
    }

    /// <summary>
    /// Compiles a formula once into a function of an environment.
    /// </summary>
    /// <param name="src">The formula.</param>
    /// <param name="names">The variables it may use (their values go in Env.Vars).</param>
    /// <returns>The formula's value for an environment (NaN and infinities are possible: x / 0).</returns>
    public static Func<Env, double> Compile(string src, IReadOnlyList<string> names)
    {
        src = src.Trim();
        var toks = Tokenize(src);
        if (toks.Count == 0)
        {
            throw new FormatException(Lang.T("shape.error.empty"));
        }
        if (toks.Count > MaxTokens)
        {
            throw new FormatException(Lang.T("shape.error.tooLong"));
        }
        return new Parser(toks, names).Parse();
    }
}
