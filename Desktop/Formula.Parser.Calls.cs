using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The formula's names: variables, pi, and calls of n(x, z) and the functions.
/// </summary>
public static partial class Formula
{
    /// <summary>
    /// The formula's names and calls as parser rules.
    /// </summary>
    internal sealed partial class Parser
    {
        /// <summary>
        /// A name after it was read: a call when a bracket follows, else pi or a variable it may use.
        /// </summary>
        internal Func<Env, double> Name(string id)
        {
            if (Eat("("))
            {
                return Call(id);
            }
            if (id == "pi")
            {
                return _ => Math.PI;
            }
            if (!_names.Contains(id))
            {
                throw new FormatException(Lang.T("shape.error.noName", id, string.Join(", ", _names)));
            }
            return env => env.Vars[id];
        }

        /// <summary>
        /// A call after its opening bracket: n(x, z) or one of the functions, with the number of arguments it takes.
        /// </summary>
        internal Func<Env, double> Call(string id)
        {
            var args = Arguments();
            if (id == "n")
            {
                return NoiseCall(args);
            }
            if (!Funcs.TryGetValue(id, out var f))
            {
                throw new FormatException(Lang.T("shape.error.noFunction", id));
            }
            int arity = Arity.GetValueOrDefault(id, 1);
            if (arity >= 0 && args.Count != arity)
            {
                throw new FormatException(arity == 1 ? Lang.T("shape.error.takesOne", id) : Lang.T("shape.error.takesMany", id, arity));
            }
            return Apply(f, [.. args]);
        }

        /// <summary>
        /// The arguments of a call, up to and including its closing bracket.
        /// </summary>
        internal List<Func<Env, double>> Arguments()
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
            return args;
        }

        /// <summary>
        /// The noise call n(x, z), which takes exactly two arguments.
        /// </summary>
        internal static Func<Env, double> NoiseCall(List<Func<Env, double>> args)
        {
            if (args.Count != 2)
            {
                throw new FormatException(Lang.T("shape.error.noiseTakesTwo"));
            }
            return env => env.Noise(args[0](env), args[1](env));
        }
    }
}
