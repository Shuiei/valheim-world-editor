using System.Text;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

// The terrain's shader (Custom/Heightmap, its deferred G-buffer pass) made into the GLSL GameLookGl
// links with the editor's vertex shader. From the OpenGL core program when the game copy has one
// (Linux, Mac); else (Valheim for Windows: Direct3D 11 and Vulkan only) from the Vulkan program, SMOL-V
// compressed and without its uniforms' names: those are put back from its parameter blob, and
// SPIRV-Cross turns it into GLSL (TerrainShader).
public static partial class GameShader
{
	public const int OpenGlCore = 15, Vulkan = 18;

	// ---- OpenGL: one fragment variant of the deferred pass, adapted.

	public static string FromOpenGl(byte[] program)
	{
		string d = Encoding.Latin1.GetString(program);
		var keep = new Dictionary<(string, bool, int, bool), string>();
		int i = 0;
		while (true)
		{
			int a = d.IndexOf("#ifdef FRAGMENT", i, StringComparison.Ordinal);
			if (a < 0)
			{
				break;
			}
			int main = d.IndexOf("void main", a, StringComparison.Ordinal);
			int b = main < 0 ? -1 : d.IndexOf("#endif\n", main, StringComparison.Ordinal);
			if (b < 0)
			{
				break;
			}
			string f = d[a..b];
			i = b;
			if (!f.Contains("_DiffuseArrayTex", StringComparison.Ordinal))
			{
				continue;
			}
			string pref = f.Contains("ds_TEXCOORD0", StringComparison.Ordinal) ? "ds" : "vs";
			bool full = f.Contains($"texture(_ClearedMaskTex, {pref}_TEXCOORD0.xy);", StringComparison.Ordinal);
			int outs = Targets().Matches(f).Select(m => m.Groups[1].Value).Distinct().Count();
			keep.TryAdd((pref, full, outs, f.Contains("unity_FogParams", StringComparison.Ordinal)), f);
		}
		string best = keep.GetValueOrDefault(("vs", true, 4, false)) ?? keep.GetValueOrDefault(("vs", true, 1, false)) ?? keep.GetValueOrDefault(("vs", true, 1, true))
			?? throw new InvalidDataException("The terrain shader has no usable fragment variant; the editor needs an update.");
		return Convert(NotPrintable().Replace(best, ""));
	}

	private const string GlHeader = """
		// Valheim's terrain shader (Custom/Heightmap, deferred G-buffer pass), compiled by Unity for
		// OpenGL core and adapted to WebGL 2: the four G-buffer targets become globals that main() lights,
		// the per-zone ocean depth and the paint mask arrive as varyings. Generated; do not edit by hand.
		precision highp float;
		precision highp int;
		precision highp sampler2D;
		precision highp sampler3D;
		precision highp sampler2DArray;

		""";

	private const string DepthBlock = """
		    u_xlat66 = _depth[2] + (-_depth[3]);
		    u_xlat66 = vs_TEXCOORD0.x * u_xlat66 + _depth[3];
		    u_xlat68 = (-_depth[0]) + _depth[1];
		    u_xlat68 = vs_TEXCOORD0.x * u_xlat68 + _depth[0];
		    u_xlat68 = (-u_xlat66) + u_xlat68;
		    u_xlat66 = vs_TEXCOORD0.y * u_xlat68 + u_xlat66;
		""";

	private static string Convert(string src)
	{
		static InvalidDataException Changed(string what) => new($"The terrain shader changed ({what}); the editor needs an update.");
		int start = src.IndexOf("uniform \tvec4 _Time;", StringComparison.Ordinal);
		if (start < 0)
		{
			throw Changed("uniforms");
		}
		string body = src[start..];
		body = body[..(body.LastIndexOf('}') + 1)];
		body = Location().Replace(body, "uniform ");
		body = Output().Replace(body, "vec4 $1;");
		body = body.Replace("uniform \tfloat _depth[4];\n", "", StringComparison.Ordinal);
		string depth = DepthBlock.Replace("\r\n", "\n", StringComparison.Ordinal);
		if (Count(body, depth) != 1)
		{
			throw Changed("depth block not found");
		}
		body = body.Replace(depth, "    u_xlat66 = vs_depth;", StringComparison.Ordinal);
		if (Count(body, "texture(_ClearedMaskTex, vs_TEXCOORD0.xy)") != 1)
		{
			throw Changed("paint mask");
		}
		body = body.Replace("texture(_ClearedMaskTex, vs_TEXCOORD0.xy)", "texture(_ClearedMaskTex, vs_maskUV)", StringComparison.Ordinal);
		if (body.Replace("in  vec2 vs_TEXCOORD0;", "", StringComparison.Ordinal).Contains("vs_TEXCOORD0", StringComparison.Ordinal))
		{
			throw Changed("texcoords");
		}
		body = body.Replace("in  vec2 vs_TEXCOORD0;", "in  float vs_depth;\nin  vec2 vs_maskUV;", StringComparison.Ordinal);
		int at = body.IndexOf("void main()", StringComparison.Ordinal);
		if (at >= 0)
		{
			body = body.Remove(at, "void main()".Length).Insert(at, "void valheimGBuffer()");
		}
		return GlHeader.Replace("\r\n", "\n", StringComparison.Ordinal) + body + "\n";
	}

	private static int Count(string s, string what)
	{
		int n = 0;
		for (int i = s.IndexOf(what, StringComparison.Ordinal); i >= 0; i = s.IndexOf(what, i + what.Length, StringComparison.Ordinal))
		{
			n++;
		}
		return n;
	}

	[GeneratedRegex(@"SV_Target(\d) =")]
	private static partial Regex Targets();

	[GeneratedRegex(@"[^\x09\x0a\x20-\x7e]")]
	private static partial Regex NotPrintable();

	[GeneratedRegex(@"UNITY_LOCATION\(\d+\) uniform ")]
	private static partial Regex Location();

	[GeneratedRegex(@"layout\(location = \d\) out vec4 (SV_Target\d);")]
	private static partial Regex Output();

	// ---- Vulkan: the deferred pass's program with no keywords and no tessellation, named.

	public const int SpirvProgram = 25;
	private const uint Fragment = 4, TessControl = 1, TessEval = 2;

	// A sub-program of the deferred pass: its GPU program type, keyword count, blob entry and parameter
	// blob entry.
	public sealed record SubProgram(int GpuProgramType, int Keywords, int BlobIndex, int ParameterBlobIndex);

	// The named SPIR-V (bytes) of the terrain's deferred fragment program, from the Vulkan blob.
	public static byte[] FromVulkan(byte[] blob, IEnumerable<SubProgram> deferred)
	{
		var entries = Entries(blob);
		foreach (var sp in deferred)
		{
			if (sp.GpuProgramType != SpirvProgram || sp.Keywords != 0 || sp.BlobIndex >= entries.Count || sp.ParameterBlobIndex >= entries.Count)
			{
				continue;
			}
			var stages = new Dictionary<uint, uint[]>();
			foreach (var (_, w) in Smolv.Programs(entries[sp.BlobIndex]))
			{
				foreach (var (_, op, a) in Instructions(w))
				{
					if (op == 15)
					{
						stages[w[a]] = w;
						break;
					}
				}
			}
			if (!stages.TryGetValue(Fragment, out var frag) || stages.ContainsKey(TessControl) || stages.ContainsKey(TessEval))
			{
				continue;
			}
			uint[] words = Named(frag, Parameters(entries[sp.ParameterBlobIndex]));
			var bytes = new byte[words.Length * 4];
			Buffer.BlockCopy(words, 0, bytes, 0, bytes.Length);
			return bytes;
		}
		throw new InvalidDataException("The terrain shader has no Vulkan program of the deferred pass without keywords; the editor needs an update.");
	}

	// The blob's entries (programs and parameter blobs).
	private static List<byte[]> Entries(byte[] blob)
	{
		int n = (int)BitConverter.ToUInt32(blob, 0);
		var res = new List<byte[]>(n);
		for (int i = 0; i < n; i++)
		{
			int o = (int)BitConverter.ToUInt32(blob, 4 + 12 * i), l = (int)BitConverter.ToUInt32(blob, 8 + 12 * i);
			res.Add(blob.AsSpan(o, l).ToArray());
		}
		return res;
	}

	// (index, op, index of the first operand, operand count) of each instruction.
	private static IEnumerable<(int At, int Op, int Args)> Instructions(uint[] words)
	{
		int i = 5;
		while (i < words.Length)
		{
			int n = (int)(words[i] >> 16);
			if (n == 0)
			{
				throw new InvalidDataException("bad SPIR-V");
			}
			yield return (i, (int)(words[i] & 0xFFFF), i + 1);
			i += n;
		}
	}

	private static int Length(uint[] words, int at) => (int)(words[at] >> 16) - 1;

	private static List<uint> StringWords(string s)
	{
		var b = Encoding.UTF8.GetBytes(s + "\0").ToList();
		while (b.Count % 4 != 0)
		{
			b.Add(0);
		}
		var res = new List<uint>();
		for (int i = 0; i < b.Count; i += 4)
		{
			res.Add(BitConverter.ToUInt32(b.ToArray(), i));
		}
		return res;
	}

	private static string ReadString(uint[] words, int from, int count)
	{
		var b = new byte[count * 4];
		Buffer.BlockCopy(words, from * 4, b, 0, b.Length);
		int end = Array.IndexOf(b, (byte)0);
		return Encoding.Latin1.GetString(b, 0, end < 0 ? b.Length : end);
	}

	public sealed record Params(Dictionary<string, Dictionary<uint, string>> Buffers, Dictionary<uint, string> Textures);

	// The names of the constant buffers' members and of the textures. Records are a length-prefixed name
	// (padded to 4 bytes) followed by numbers: a constant buffer (size, member count), a member (type,
	// rows, columns, is matrix, array size, offset), a texture (0, binding with flags in the top byte,
	// sampler, dimension).
	internal static Params Parameters(byte[] entry)
	{
		var recs = new List<(string Name, List<uint> Nums)>();
		int p = 24;
		while (p + 4 <= entry.Length)
		{
			uint n = BitConverter.ToUInt32(entry, p);
			if (n is > 0 and < 128 && p + 4 + n <= entry.Length && IsName(entry.AsSpan(p + 4, (int)n)))
			{
				recs.Add((Encoding.ASCII.GetString(entry, p + 4, (int)n), new List<uint>()));
				p = p + 4 + (int)n + (int)((4 - n % 4) % 4);
				continue;
			}
			if (recs.Count > 0)
			{
				recs[^1].Nums.Add(n);
			}
			p += 4;
		}
		var buffers = new Dictionary<string, Dictionary<uint, string>>();
		var textures = new Dictionary<uint, string>();
		int i = 0;
		while (i < recs.Count)
		{
			var (name, nums) = recs[i];
			bool globals = name.Contains("Globals", StringComparison.Ordinal);
			if (globals && nums.Count >= 2 && nums[1] > 0 && nums[1] < recs.Count - i && recs.Skip(i + 1).Take((int)nums[1]).All(r => r.Nums.Count >= 6))
			{
				var members = new Dictionary<uint, string>();
				foreach (var (mname, m) in recs.Skip(i + 1).Take((int)nums[1]))
				{
					members[m[5]] = mname;
				}
				buffers[name] = members;
				i += 1 + (int)nums[1];
				continue;
			}
			if (!globals && nums.Count >= 4 && nums[0] == 0)
			{
				textures[nums[1] & 0xFFFF] = name;
			}
			i++;
		}
		return new Params(buffers, textures);
	}

	private static bool IsName(ReadOnlySpan<byte> s)
	{
		if (s.Length == 0 || !(char.IsAsciiLetter((char)s[0]) || s[0] == '_'))
		{
			return false;
		}
		foreach (byte c in s)
		{
			if (!(char.IsAsciiLetterOrDigit((char)c) || c == '_'))
			{
				return false;
			}
		}
		return true;
	}

	// The SPIR-V with names for the uniform buffer's members, the textures, the inputs (the unnamed one
	// is vs_COLOR0, as in the OpenGL program) and the outputs (SV_Target0..3).
	internal static uint[] Named(uint[] words, Params prm)
	{
		var names = new Dictionary<uint, string>();
		var deco = new Dictionary<uint, Dictionary<uint, uint[]>>();
		var memberOff = new Dictionary<uint, Dictionary<uint, uint>>();
		var pointer = new Dictionary<uint, (uint Storage, uint Type)>();
		var variables = new List<(uint Var, uint Ptype, uint Storage)>();
		var block = new HashSet<uint>();
		foreach (var (at, op, a) in Instructions(words))
		{
			int len = Length(words, at);
			switch (op)
			{
				case 5:
					names[words[a]] = ReadString(words, a + 1, len - 1);
					break;
				case 71:
					if (!deco.TryGetValue(words[a], out var d))
					{
						deco[words[a]] = d = new Dictionary<uint, uint[]>();
					}
					d[words[a + 1]] = words.AsSpan(a + 2, len - 2).ToArray();
					if (words[a + 1] == 2)
					{
						block.Add(words[a]);
					}
					break;
				case 72 when words[a + 2] == 35:
					if (!memberOff.TryGetValue(words[a], out var mo))
					{
						memberOff[words[a]] = mo = new Dictionary<uint, uint>();
					}
					mo[words[a + 1]] = words[a + 3];
					break;
				case 32:
					pointer[words[a]] = (words[a + 1], words[a + 2]);
					break;
				case 59:
					variables.Add((words[a + 1], words[a], words[a + 2]));
					break;
			}
		}
		var add = new List<(uint Op, List<uint> Words)>();
		foreach (var (v, ptype, storage) in variables)
		{
			uint st = pointer.TryGetValue(ptype, out var pt) ? pt.Type : uint.MaxValue;
			if (storage != 2 || !block.Contains(st))
			{
				continue;
			}
			var offs = memberOff.GetValueOrDefault(st) ?? new Dictionary<uint, uint>();
			var buf = prm.Buffers.Values.FirstOrDefault(b => offs.Values.All(b.ContainsKey))
				?? throw new InvalidDataException("The terrain shader's uniform buffer matches none of its parameter blob's.");
			foreach (var (idx, o) in offs)
			{
				add.Add((6, new List<uint> { st, idx }.Concat(StringWords(buf[o])).ToList()));
			}
			add.Add((5, new List<uint> { v }.Concat(StringWords("_Globals")).ToList()));
		}
		foreach (var (v, _, storage) in variables)
		{
			var d = deco.GetValueOrDefault(v) ?? new Dictionary<uint, uint[]>();
			if (storage == 0 && d.TryGetValue(33, out var binding))
			{
				string name = prm.Textures.GetValueOrDefault(binding[0]) ?? throw new InvalidDataException($"The terrain shader has no texture for binding {binding[0]}.");
				add.Add((5, new List<uint> { v }.Concat(StringWords(name)).ToList()));
			}
			else if (storage == 1 && d.ContainsKey(30) && !names.ContainsKey(v))
			{
				add.Add((5, new List<uint> { v }.Concat(StringWords("vs_COLOR0")).ToList()));
			}
			else if (storage == 3 && d.TryGetValue(30, out var loc))
			{
				add.Add((5, new List<uint> { v }.Concat(StringWords($"SV_Target{loc[0]}")).ToList()));
			}
		}
		// Names go after the existing debug names, before the first decoration.
		var output = new List<uint>(words.Length + 64);
		output.AddRange(words.AsSpan(0, 5).ToArray());
		bool done = false;
		foreach (var (at, op, _) in Instructions(words))
		{
			if (!done && op is 71 or 72 or 73 or 74 or 75 or 332 or 5632 or 5633)
			{
				foreach (var (o, ws) in add)
				{
					output.Add(((uint)(ws.Count + 1) << 16) | o);
					output.AddRange(ws);
				}
				done = true;
			}
			output.AddRange(words.AsSpan(at, Length(words, at) + 1).ToArray());
		}
		return output.ToArray();
	}
}
