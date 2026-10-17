namespace TerrainEditor.Desktop;

/// <summary>
/// The formula's comparisons and logical operators as parser rules: each sum compared, then &amp;&amp; and ||.
/// </summary>
public static partial class Formula
{
    /// <summary>
    /// The formula's comparisons and logical operators as parser rules.
    /// </summary>
    private sealed partial class Parser
    {
        /// <summary>
        /// Comparisons, each of two sums, giving 1 when it holds and 0 when not.
        /// </summary>
        private Func<Env, double> Compare()
        {
            var a = Sum();
            while (Peek()?.Op is { } op && Comparisons.ContainsKey(op))
            {
                _i++;
                var holds = Comparisons[op];
                var b = Sum();
                var l = a;
                a = env => holds(l(env), b(env)) ? 1 : 0;
            }
            return a;
        }

        /// <summary>
        /// &amp;&amp; binds tighter than ||, as in JavaScript: 1 || 0 &amp;&amp; 0 is 1. Like JavaScript, each gives the deciding value
        /// itself, not 1.
        /// </summary>
        private Func<Env, double> And()
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

        /// <summary>
        /// Or: gives the first value that is true, else the last.
        /// </summary>
        private Func<Env, double> Or()
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
    }
}
