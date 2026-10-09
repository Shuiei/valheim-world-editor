using System.Globalization;
using System.Text.RegularExpressions;

namespace TerrainEditor.Desktop;

// A small, safe formula language, the same as the web editor's (editor/shapes.js compile): numbers,
// named variables, + - * / % ^, comparisons, && ||, !, a ? b : c, pi, n(x, z) (smooth noise) and the
// functions below. Compiled once into a function of an environment; a mistake throws a
// FormatException with a plain message.
public static class Formula
{
	public sealed class Env
	{
		public Dictionary<string, double> Vars { get; } = new();
		public Func<double, double, double> Noise { get; set; } = (_, _) => 0;
	}

	// JavaScript's truth: 0 and NaN are false.
	private static bool Truthy(double v) => v != 0 && !double.IsNaN(v);

	private static double Smooth(double t)
	{
		t = Math.Clamp(t, 0, 1);
		return t * t * (3 - 2 * t);
	}

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

	// How many numbers each function takes (min and max: any number, none included, as in JavaScript).
	private static readonly Dictionary<string, int> Arity = new()
	{
		["pow"] = 2, ["atan2"] = 2, ["clamp"] = 3, ["min"] = -1, ["max"] = -1,
	};

	// Deeper nesting, or a longer formula, would run out of stack (which closes the app: it cannot be
	// caught) when it is read or worked out.
	private const int MaxDepth = 100, MaxTokens = 4000;

	private sealed record Tok(double? N = null, string? Id = null, string? Op = null)
	{
		public override string ToString() => Op ?? Id ?? N?.ToString(CultureInfo.InvariantCulture) ?? "";
	}

	private static readonly Regex Token = new(@"\G\s*(?:(\d+\.?\d*(?:e[+-]?\d+)?|\.\d+)|([A-Za-z_]\w*)|(<=|>=|==|!=|&&|\|\||[-+*/%^(),<>?:!]))", RegexOptions.Compiled);

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
					return env => f(arr.Select(a => a(env)).ToArray());
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
		Func<Env, double> Logic()
		{
			var a = Compare();
			while (Peek()?.Op is "&&" or "||")
			{
				string op = toks[i++].Op!;
				var b = Compare();
				var l = a;
				// Like JavaScript: the deciding value itself, not 1.
				a = op == "&&" ? env => l(env) is var x && Truthy(x) ? b(env) : x : env => l(env) is var y && Truthy(y) ? y : b(env);
			}
			return a;
		}
		Func<Env, double> Ternary()
		{
			var c = Logic();
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
