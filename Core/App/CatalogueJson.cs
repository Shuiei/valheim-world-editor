using System.Globalization;
using System.Numerics;
using System.Text;

namespace TerrainEditor.App;

// The JSON of the game data read at the editor's start (GameCatalogues), written the way the
// catalogues once kept in Core/WorldGen were (Python's json module), so each kind reads exactly as its
// catalogue did: floats in their shortest form ("1.0", "0.30000001192092896", "1e-05"), numbers
// rounded half to even, keys in the order they were added (or sorted), the same separators and line
// breaks. Values: null, bool, long (ints), double (floats), string, IList, JsonDict.
internal static class CatalogueJson
{
	public sealed record Style(string ItemSep = ", ", string KeySep = ": ", int? Indent = null, bool SortKeys = false, bool Ascii = true);

	// A dictionary that keeps its keys in the order they came (setting a key that is there keeps its place).
	public sealed class Dict : IEnumerable<KeyValuePair<string, object?>>
	{
		private readonly List<string> _keys = new();
		private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

		public object? this[string key]
		{
			get => _values[key];
			set
			{
				if (!_values.ContainsKey(key))
				{
					_keys.Add(key);
				}
				_values[key] = value;
			}
		}

		public int Count => _keys.Count;

		public bool ContainsKey(string key) => _values.ContainsKey(key);

		public bool TryGetValue(string key, out object? value) => _values.TryGetValue(key, out value);

		public void Remove(string key)
		{
			if (_values.Remove(key))
			{
				_keys.Remove(key);
			}
		}

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _keys.Select(k => new KeyValuePair<string, object?>(k, _values[k])).GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}

	public static string Write(object? value, Style style)
	{
		var sb = new StringBuilder();
		Append(sb, value, style, 0);
		return sb.ToString();
	}

	private static void Append(StringBuilder sb, object? v, Style style, int level)
	{
		switch (v)
		{
			case null:
				sb.Append("null");
				break;
			case bool b:
				sb.Append(b ? "true" : "false");
				break;
			case int i:
				sb.Append(i.ToString(CultureInfo.InvariantCulture));
				break;
			case long l:
				sb.Append(l.ToString(CultureInfo.InvariantCulture));
				break;
			case double d:
				sb.Append(Repr(d));
				break;
			case string s:
				Quote(sb, s, style.Ascii);
				break;
			case Dict dict:
				var items = style.SortKeys ? dict.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList() : dict.ToList();
				Container(sb, '{', '}', items.Count, style, level, (k, lv) =>
				{
					Quote(sb, items[k].Key, style.Ascii);
					sb.Append(style.KeySep);
					Append(sb, items[k].Value, style, lv);
				});
				break;
			case System.Collections.IList list:
				Container(sb, '[', ']', list.Count, style, level, (k, lv) => Append(sb, list[k], style, lv));
				break;
			default:
				throw new ArgumentException($"no JSON for a {v.GetType().Name}");
		}
	}

	// A list or dictionary: with an indent, each item on a line of its own (Python's indent=0: no spaces).
	private static void Container(StringBuilder sb, char open, char close, int count, Style style, int level, Action<int, int> item)
	{
		if (count == 0)
		{
			sb.Append(open).Append(close);
			return;
		}
		sb.Append(open);
		string? newline = style.Indent is int n ? "\n" + new string(' ', n * (level + 1)) : null;
		sb.Append(newline);
		for (int k = 0; k < count; k++)
		{
			if (k > 0)
			{
				sb.Append(style.ItemSep).Append(newline);
			}
			item(k, level + 1);
		}
		if (style.Indent is int m)
		{
			sb.Append('\n').Append(' ', m * level);
		}
		sb.Append(close);
	}

	private static void Quote(StringBuilder sb, string s, bool ascii)
	{
		sb.Append('"');
		foreach (char c in s)
		{
			switch (c)
			{
				case '"': sb.Append("\\\""); break;
				case '\\': sb.Append("\\\\"); break;
				case '\n': sb.Append("\\n"); break;
				case '\r': sb.Append("\\r"); break;
				case '\t': sb.Append("\\t"); break;
				case '\b': sb.Append("\\b"); break;
				case '\f': sb.Append("\\f"); break;
				default:
					if (c < ' ' || ascii && c > '~')
					{
						sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						sb.Append(c);
					}
					break;
			}
		}
		sb.Append('"');
	}

	// A float as Python prints it: the shortest digits that read back the same, with ".0" on whole
	// numbers, and an exponent below 1e-4 and from 1e16.
	public static string Repr(double d)
	{
		if (double.IsNaN(d))
		{
			return "NaN";
		}
		if (double.IsInfinity(d))
		{
			return d > 0 ? "Infinity" : "-Infinity";
		}
		string sign = double.IsNegative(d) ? "-" : "";
		if (d == 0)
		{
			return sign + "0.0";
		}
		string r = Math.Abs(d).ToString("R", CultureInfo.InvariantCulture);
		int e = 0, ei = r.IndexOf('E', StringComparison.Ordinal);
		if (ei >= 0)
		{
			e = int.Parse(r.AsSpan(ei + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
			r = r[..ei];
		}
		int dot = r.IndexOf('.', StringComparison.Ordinal);
		string digits = dot < 0 ? r : r.Remove(dot, 1);
		// The decimal point's place after the first digit.
		int point = (dot < 0 ? r.Length : dot) + e;
		int lead = 0;
		while (lead < digits.Length - 1 && digits[lead] == '0')
		{
			lead++;
		}
		digits = digits[lead..].TrimEnd('0');
		point -= lead;
		int exp = point - 1;
		if (exp < -4 || exp >= 16)
		{
			string mant = digits.Length > 1 ? digits[..1] + "." + digits[1..] : digits;
			return $"{sign}{mant}e{(exp < 0 ? '-' : '+')}{Math.Abs(exp):00}";
		}
		if (point <= 0)
		{
			return $"{sign}0.{new string('0', -point)}{digits}";
		}
		if (point >= digits.Length)
		{
			return $"{sign}{digits}{new string('0', point - digits.Length)}.0";
		}
		return $"{sign}{digits[..point]}.{digits[point..]}";
	}

	private static readonly double[] Pow10 = Enumerable.Range(0, 23).Select(i => Math.Pow(10, i)).ToArray();

	// Python's round(x, n): the exact value rounded half to even at n decimals, read back as the nearest
	// double (the sign kept, -0.0 included).
	public static double Round(double x, int n)
	{
		if (!double.IsFinite(x) || x == 0)
		{
			return x;
		}
		long bits = BitConverter.DoubleToInt64Bits(Math.Abs(x));
		int be = (int)((bits >> 52) & 0x7FF);
		long mant = bits & 0xFFFFFFFFFFFFFL;
		if (be == 0)
		{
			be = 1;
		}
		else
		{
			mant |= 1L << 52;
		}
		int shift = 1075 - be;
		if (shift <= 0)
		{
			// A whole number already.
			return x;
		}
		// |x| = mant / 2^shift; scaled = mant * 10^n / 2^shift, rounded half to even.
		BigInteger num = new BigInteger(mant) * BigInteger.Pow(10, n);
		BigInteger q = num >> shift;
		BigInteger rem = num - (q << shift);
		BigInteger half = BigInteger.One << (shift - 1);
		if (rem > half || rem == half && !q.IsEven)
		{
			q += 1;
		}
		double r = q < (BigInteger.One << 53) && n < Pow10.Length
			? (double)q / Pow10[n]
			: double.Parse($"{q}E-{n}", CultureInfo.InvariantCulture);
		return double.IsNegative(x) ? -r : r;
	}

	// Python's float("%.9G" % x): x to 9 significant digits (UnityPy's mesh export writes vertices so).
	public static double Significant9(double x)
	{
		if (!double.IsFinite(x) || x == 0)
		{
			return x;
		}
		int e = (int)Math.Floor(Math.Log10(Math.Abs(x)));
		return e <= 8 ? Round(x, 8 - e) : double.Parse(x.ToString("E8", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
	}
}
