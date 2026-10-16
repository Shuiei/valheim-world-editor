using System.Globalization;
using System.Text.RegularExpressions;

namespace TerrainEditor.Desktop;

/// <summary>
/// A small, safe formula language, the same as the web editor's (editor/shapes.js compile): numbers, named variables, +
/// - * / % ^, comparisons, &amp;&amp; ||, !, a ? b : c, pi, n(x, z) (smooth noise) and the functions below. Compiled
/// once into a function of an environment; a mistake throws a FormatException with a plain message.
/// </summary>
public static class Formula
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
        ["sign"] = a => Math.Sign(a[0]),
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
    /// A token of the formula: a number, a name or an operator (only one is set).
    /// </summary>
    /// <param name="N">A number.</param>
    /// <param name="Id">A name (variable, function, pi).</param>
    /// <param name="Op">An operator or bracket.</param>
    private sealed record Tok(double? N = null, string? Id = null, string? Op = null)
    {
        /// <summary>
        /// The token as it is written, for messages.
        /// </summary>
        public override string ToString() => Op ?? Id ?? N?.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>
    /// One token from where it is asked for (\G), spaces before it skipped: a number, a name, or an operator.
    /// </summary>
    private static readonly Regex Token = new(@"\G\s*(?:(\d+\.?\d*(?:e[+-]?\d+)?|\.\d+)|([A-Za-z_]\w*)|(<=|>=|==|!=|&&|\|\||[-+*/%^(),<>?:!]))", RegexOptions.Compiled);

    /// <summary>
    /// Compiles a formula once into a function of an environment.
    /// </summary>
    /// <param name="src">The formula.</param>
    /// <param name="names">The variables it may use (their values go in Env.Vars).</param>
    /// <returns>The formula's value for an environment (NaN and infinities are possible: x / 0).</returns>
    public static Func<Env, double> Compile(string src, IReadOnlyList<string> names)
    {
        src = src.Trim();
        var toks = new List<Tok>();
        int pos = 0;
        while (pos < src.Length)
        {
            var m = Token.Match(src, pos);
            if (!m.Success || m.Length == 0)
            {
                string rest = src[pos..].Trim();
                throw new FormatException($"I do not understand “{rest[..Math.Min(12, rest.Length)]}”");
            }
            toks.Add(m.Groups[1].Success ? new Tok(N: double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                : m.Groups[2].Success ? new Tok(Id: m.Groups[2].Value) : new Tok(Op: m.Groups[3].Value));
            pos = m.Index + m.Length;
        }
        if (toks.Count == 0)
        {
            throw new FormatException("the formula is empty");
        }
        if (toks.Count > MaxTokens)
        {
            throw new FormatException("the formula is too long");
        }
        int i = 0, depth = 0;
        Tok? Peek() => i < toks.Count ? toks[i] : null;
        bool Eat(string op)
        {
            if (Peek()?.Op == op)
            {
                i++;
                return true;
            }
            return false;
        }
        void Need(string op)
        {
            if (!Eat(op))
            {
                throw new FormatException($"“{op}” expected{(Peek() is { } t ? $" before “{t}”" : " at the end")}");
            }
        }
        Func<Env, double> Primary()
        {
            if (++depth > MaxDepth)
            {
                throw new FormatException("the formula is nested too deeply");
            }
            try
            {
                return PrimaryAt();
            }
            finally
            {
                depth--;
            }
        }
        Func<Env, double> PrimaryAt()
        {
            if (i >= toks.Count)
            {
                throw new FormatException("the formula ends too early");
            }
            var t = toks[i++];
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
                if (Eat("("))
                {
                    var args = new List<Func<Env, double>>();
                    if (!Eat(")"))
                    {
                        do
                        {
                            args.Add(Ternary());
                        }
                        while (Eat(","));
                        Need(")");
                    }
                    if (id == "n")
                    {
                        if (args.Count != 2)
                        {
                            throw new FormatException("n(x, z) takes two numbers");
                        }
                        return env => env.Noise(args[0](env), args[1](env));
                    }
                    if (!Funcs.TryGetValue(id, out var f))
                    {
                        throw new FormatException($"there is no function “{id}”");
                    }
                    int arity = Arity.GetValueOrDefault(id, 1);
                    if (arity >= 0 && args.Count != arity)
                    {
                        throw new FormatException($"{id}(…) takes {(arity == 1 ? "one number" : $"{arity} numbers")}");
                    }
                    var arr = args.ToArray();
                    return env => f([.. arr.Select(a => a(env))]);
                }
                if (id == "pi")
                {
                    return _ => Math.PI;
                }
                if (!names.Contains(id))
                {
                    throw new FormatException($"there is no “{id}” (you can use {string.Join(", ", names)}, pi)");
                }
                return env => env.Vars[id];
            }
            throw new FormatException($"“{t.Op}” is out of place");
        }
        Func<Env, double> Power()
        {
            var a = Primary();
            if (Eat("^"))
            {
                var b = Power();
                return env => Math.Pow(a(env), b(env));
            }
            return a;
        }
        Func<Env, double> Product()
        {
            var a = Power();
            while (Peek()?.Op is "*" or "/" or "%")
            {
                string op = toks[i++].Op!;
                var b = Power();
                var l = a;
                a = op == "*" ? env => l(env) * b(env) : op == "/" ? env => l(env) / b(env) : env => l(env) % b(env);
            }
            return a;
        }
        Func<Env, double> Sum()
        {
            var a = Product();
            while (Peek()?.Op is "+" or "-")
            {
                string op = toks[i++].Op!;
                var b = Product();
                var l = a;
                a = op == "+" ? env => l(env) + b(env) : env => l(env) - b(env);
            }
            return a;
        }
        Func<Env, double> Compare()
        {
            var a = Sum();
            while (Peek()?.Op is "<" or ">" or "<=" or ">=" or "==" or "!=")
            {
                string op = toks[i++].Op!;
                var b = Sum();
                var l = a;
                a = op switch
                {
                    "<" => env => l(env) < b(env) ? 1 : 0,
                    ">" => env => l(env) > b(env) ? 1 : 0,
                    "<=" => env => l(env) <= b(env) ? 1 : 0,
                    ">=" => env => l(env) >= b(env) ? 1 : 0,
                    "==" => env => l(env) == b(env) ? 1 : 0,
                    _ => env => l(env) != b(env) ? 1 : 0,
                };
            }
            return a;
        }
        // && binds tighter than ||, as in JavaScript: 1 || 0 && 0 is 1. Like JavaScript, each gives the deciding value
        // itself, not 1.
        Func<Env, double> And()
        {
            var a = Compare();
            while (Eat("&&"))
            {
                var b = Compare();
                var l = a;
                a = env => l(env) is var x && Truthy(x) ? b(env) : x;
            }
            return a;
        }
        Func<Env, double> Or()
        {
            var a = And();
            while (Eat("||"))
            {
                var b = And();
                var l = a;
                a = env => l(env) is var y && Truthy(y) ? y : b(env);
            }
            return a;
        }
        Func<Env, double> Ternary()
        {
            var c = Or();
            if (Eat("?"))
            {
                var a = Ternary();
                Need(":");
                var b = Ternary();
                return env => Truthy(c(env)) ? a(env) : b(env);
            }
            return c;
        }
        var result = Ternary();
        if (i < toks.Count)
        {
            throw new FormatException($"“{toks[i]}” is out of place");
        }
        return result;
    }
}
