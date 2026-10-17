namespace TerrainEditor.Desktop;

/// <summary>
/// The formula's operators, from the tightest binding up: ^, * / %, + -, comparisons, &amp;&amp;, ||, and the ?: choice.
/// </summary>
public static partial class Formula
{
    /// <summary>
    /// The formula's operators as parser rules.
    /// </summary>
    internal sealed partial class Parser
    {
        /// <summary>
        /// A power: the base, then ^ and the exponent (right to left).
        /// </summary>
        internal Func<Env, double> Power()
        {
            var a = Primary();
            if (Eat("^"))
            {
                var b = Power();
                return env => Math.Pow(a(env), b(env));
            }
            return a;
        }

        /// <summary>
        /// A chain of one level's operators, left to right: each operand is read by operand, each operator combined by ops.
        /// </summary>
        /// <param name="operand">Reads one operand of the level.</param>
        /// <param name="ops">The level's operators, by sign.</param>
        internal Func<Env, double> Chain(Func<Func<Env, double>> operand, Dictionary<string, Func<double, double, double>> ops)
        {
            var a = operand();
            while (Peek()?.Op is { } op && ops.ContainsKey(op))
            {
                _i++;
                var apply = ops[op];
                var b = operand();
                var l = a;
                a = env => apply(l(env), b(env));
            }
            return a;
        }

        /// <summary>
        /// Products and remainders.
        /// </summary>
        internal Func<Env, double> Product() => Chain(Power, Products);

        /// <summary>
        /// Sums and differences.
        /// </summary>
        internal Func<Env, double> Sum() => Chain(Product, Sums);

        /// <summary>
        /// The choice a ? b : c, the loosest operator.
        /// </summary>
        internal Func<Env, double> Ternary()
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
    }
}
