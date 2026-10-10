namespace TerrainEditor.App;

// SMOL-V to SPIR-V, for Unity's Vulkan shaders (Unity keeps each Vulkan program SMOL-V compressed): a
// port of the decoder in SMOL-V by Aras Pranckevicius (https://github.com/aras-p/smol-v, MIT license,
// Copyright (c) 2016-2024 Aras Pranckevicius), encoding versions 0 and 1.
public static class Smolv
{
	// Per SPIR-V op (0..366), four digits: has a result id, has a type id, how many ids after them are
	// kept as deltas from the result, whether the other words are varints (SMOL-V's kSpirvOpData).
	private const string Table =
		"000011000000000100000000000000000001110000001000110111210001000100010001110010011001100110011001" +
		"100110011001100110011001100110011001100110011001100110011001000111001100110011001190110111001100" +
		"110011001100119011001100110111000000119011001101110011110021000000001101110011001100110011000001" +
		"000110000000000011001111112111211190111111211110110011001100112111211131113111211121113111311121" +
		"113111311121003111101110111011201110112011101110110011101110111011101110111011101110111011101110" +
		"111011101110111111101100111011101120112011201120112011201120112011201120112011201120112011201120" +
		"112011201120112011201120112011201120110011101110111011101110111011101120112011201120112011201120" +
		"111011301120112011201120112011201120112011201120112011201120112011201120112011201120112011201120" +
		"110011001120112011201120112011201110114011301130111011101100110011001100110011001100110011001100" +
		"110011000000000000000000110011000030002011001100000011001100110011001100110011001100110011001100" +
		"110011001100110011001100002100111000001000310000000000000000000000000000110011000000110011001100" +
		"110011001100110011001100110011001100110011001100110011001100110000000000110011001100110011000000" +
		"000011001100110011001100110011001100000000001100110000000000110011001121112111311131112111211131" +
		"113111211131113111100000110000001100110011001100110011001100110011010021110000010001111111111111" +
		"111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111" +
		"1111111111111111111111111111";

	// The ops each encoding version knows (version 0: up to OpModuleProcessed).
	private static readonly Dictionary<int, int> Known = new() { [0] = 331, [1] = 367 };

	private const uint SpirvMagic = 0x07230203;
	private static readonly byte[] Magic = "LOMS"u8.ToArray();

	private const int Shuffle = 79, ShuffleCompact = 13, Decorate = 71, MemberDecorate = 72, Load = 61, AccessChain = 65;

	// The most common ops are swapped with rare small ones, to fit a one-byte varint.
	private static readonly Dictionary<int, int> Swap = BuildSwap();

	private static Dictionary<int, int> BuildSwap()
	{
		var s = new Dictionary<int, int>();
		foreach (var (a, b) in new[] { (71, 0), (61, 1), (62, 2), (65, 3), (79, 4), (72, 7), (248, 8), (59, 9), (133, 10), (129, 11), (32, 14), (127, 15) })
		{
			s[a] = b;
			s[b] = a;
		}
		return s;
	}

	private static (int Result, int Type, int Deltas, int Varints) Op(int op) =>
		(Table[op * 4] - '0', Table[op * 4 + 1] - '0', Table[op * 4 + 2] - '0', Table[op * 4 + 3] - '0');

	// The SPIR-V size in bytes of the SMOL-V program at data[at..], or 0 when it is not one.
	public static int DecodedSize(byte[] data, int at = 0) =>
		at + 24 <= data.Length && data.AsSpan(at, 4).SequenceEqual(Magic) ? (int)BitConverter.ToUInt32(data, at + 20) : 0;

	// The SPIR-V words of the SMOL-V program starting at data[at..] (data may hold more after it).
	public static uint[] Decode(byte[] data, int at = 0)
	{
		int size = DecodedSize(data, at);
		if (size == 0)
		{
			throw new InvalidDataException("not SMOL-V data");
		}
		uint version = BitConverter.ToUInt32(data, at + 4), generator = BitConverter.ToUInt32(data, at + 8), bound = BitConverter.ToUInt32(data, at + 12), schema = BitConverter.ToUInt32(data, at + 16);
		int smolVersion = (int)(version >> 24);
		if (!Known.TryGetValue(smolVersion, out int known))
		{
			throw new InvalidDataException($"SMOL-V encoding version {smolVersion} is not known");
		}
		var output = new List<uint>(size / 4) { SpirvMagic, version & 0xFFFFFF, generator, bound, schema };
		int want = size / 4, p = at + 24, n = data.Length;

		uint Varint()
		{
			uint v = 0;
			int shift = 0;
			while (p < n)
			{
				byte b = data[p++];
				v |= (uint)(b & 127) << shift;
				shift += 7;
				if ((b & 128) == 0)
				{
					break;
				}
			}
			return v;
		}
		static uint Zig(uint u) => (u & 1) != 0 ? (u >> 1) ^ 0xFFFFFFFF : u >> 1;
		uint Word()
		{
			if (p + 4 > n)
			{
				throw new InvalidDataException("SMOL-V data ends early");
			}
			uint v = BitConverter.ToUInt32(data, p);
			p += 4;
			return v;
		}

		uint prevResult = 0, prevDecorate = 0;
		while (output.Count < want)
		{
			if (p >= n)
			{
				throw new InvalidDataException("SMOL-V data ends early");
			}
			uint v = Varint();
			int length = (int)(((v >> 20) << 4) | ((v >> 4) & 0xF));
			int raw = (int)(((v >> 4) & 0xFFF0) | (v & 0xF));
			int op = Swap.TryGetValue(raw, out int swapped) ? swapped : raw;
			length += 1;
			if (op is Shuffle or ShuffleCompact)
			{
				length += 4;
			}
			else if (op == Decorate)
			{
				length += 2;
			}
			else if (op is Load or AccessChain)
			{
				length += 3;
			}
			bool swizzle = op == ShuffleCompact;
			if (swizzle)
			{
				op = Shuffle;
			}
			output.Add(((uint)length << 16) | (uint)op);
			var info = op < known ? Op(op) : (Result: 0, Type: 0, Deltas: 0, Varints: 0);
			int i = 1;
			if (info.Type != 0)
			{
				output.Add(Varint());
				i++;
			}
			if (info.Result != 0)
			{
				prevResult += Zig(Varint());
				output.Add(prevResult);
				i++;
			}
			if (op is Decorate or MemberDecorate)
			{
				prevDecorate += Zig(Varint());
				output.Add(prevDecorate);
				i++;
			}
			if (op == MemberDecorate)
			{
				int count = data[p++];
				uint prevIndex = 0, prevOffset = 0;
				for (int m = 0; m < count; m++)
				{
					uint index = Varint() + prevIndex;
					prevIndex = index;
					uint dec = Varint();
					int extra = dec == 0 || dec is >= 2 and <= 5 ? 0 : dec is >= 29 and <= 37 ? 1 : -1;
					int mlen = extra < 0 ? (int)Varint() + 4 : 4 + extra;
					if (m > 0)
					{
						output.Add(((uint)mlen << 16) | (uint)op);
						output.Add(prevDecorate);
					}
					output.Add(index);
					output.Add(dec);
					if (dec == 35)
					{
						// Offset
						if (mlen != 5)
						{
							throw new InvalidDataException("bad SMOL-V member offset");
						}
						prevOffset = Varint() + prevOffset;
						output.Add(prevOffset);
					}
					else
					{
						for (int k = 4; k < mlen; k++)
						{
							output.Add(Varint());
						}
					}
				}
				continue;
			}
			for (int k = 0; k < info.Deltas && i < length; k++, i++)
			{
				output.Add(prevResult - Zig(Varint()));
			}
			if (swizzle && length <= 9)
			{
				byte s = data[p++];
				foreach (var (at2, sh) in new[] { (5, 6), (6, 4), (7, 2), (8, 0) })
				{
					if (length > at2)
					{
						output.Add((uint)(s >> sh) & 3);
					}
				}
			}
			else if (info.Varints != 0)
			{
				for (; i < length; i++)
				{
					output.Add(Varint());
				}
			}
			else
			{
				for (; i < length; i++)
				{
					output.Add(Word());
				}
			}
		}
		if (output.Count != want)
		{
			throw new InvalidDataException("SMOL-V data decodes to the wrong size");
		}
		return output.ToArray();
	}

	// (offset, SPIR-V words) of every SMOL-V program in a blob.
	public static IEnumerable<(int At, uint[] Words)> Programs(byte[] data)
	{
		int at = data.AsSpan().IndexOf(Magic);
		while (at >= 0)
		{
			if (DecodedSize(data, at) > 0)
			{
				yield return (at, Decode(data, at));
			}
			int next = data.AsSpan(at + 4).IndexOf(Magic);
			at = next < 0 ? -1 : at + 4 + next;
		}
	}
}
