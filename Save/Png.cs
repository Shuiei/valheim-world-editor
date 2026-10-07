using System.IO.Compression;
using System.Text;

namespace TerrainEditor.Save;

// Just enough PNG for heightmaps: writes 16-bit grayscale pictures (with text notes), and reads the
// pictures other programs make (gray, gray + alpha, RGB, RGBA; 8 or 16 bits; not interlaced), as
// brightness from 0 to 1. Browsers can only read PNGs as 8 bits, so 16-bit heightmaps go through here.
public static class Png
{
	private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

	public sealed record Image(int Width, int Height, float[] Values, Dictionary<string, string> Text);

	// values: width * height, row by row from the top, 0..65535. filter: the PNG row filter to use
	// (0 none ... 4 Paeth; the default 1 Sub compresses heightmaps well).
	public static byte[] WriteGray16(int width, int height, ushort[] values, IReadOnlyDictionary<string, string>? text = null, int filter = 1)
	{
		using MemoryStream png = new();
		png.Write(Signature);
		byte[] ihdr = new byte[13];
		BigEndian(ihdr, 0, width);
		BigEndian(ihdr, 4, height);
		ihdr[8] = 16;  // bit depth
		ihdr[9] = 0;   // grayscale
		Chunk(png, "IHDR", ihdr);
		foreach (var (k, v) in text ?? new Dictionary<string, string>())
		{
			Chunk(png, "tEXt", Encoding.Latin1.GetBytes(k + "\0" + v));
		}
		int stride = width * 2;
		byte[] raw = new byte[(stride + 1) * height], prev = new byte[stride], row = new byte[stride];
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				ushort v = values[y * width + x];
				row[x * 2] = (byte)(v >> 8);
				row[x * 2 + 1] = (byte)v;
			}
			int o = y * (stride + 1);
			raw[o] = (byte)filter;
			for (int i = 0; i < stride; i++)
			{
				int a = i >= 2 ? row[i - 2] : 0, b = prev[i], c = i >= 2 ? prev[i - 2] : 0;
				raw[o + 1 + i] = (byte)(row[i] - filter switch { 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a, b, c), _ => 0 });
			}
			(prev, row) = (row, prev);
		}
		using (MemoryStream z = new())
		{
			using (ZLibStream zs = new(z, CompressionLevel.Optimal, leaveOpen: true))
			{
				zs.Write(raw);
			}
			Chunk(png, "IDAT", z.ToArray());
		}
		Chunk(png, "IEND", Array.Empty<byte>());
		return png.ToArray();
	}

	public static Image Read(byte[] bytes)
	{
		if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(Signature))
		{
			throw new InvalidDataException("not a PNG picture");
		}
		int width = 0, height = 0, depth = 0, color = 0;
		Dictionary<string, string> text = new();
		using MemoryStream idat = new();
		int p = 8;
		while (p + 8 <= bytes.Length)
		{
			int len = (bytes[p] << 24) | (bytes[p + 1] << 16) | (bytes[p + 2] << 8) | bytes[p + 3];
			string type = Encoding.ASCII.GetString(bytes, p + 4, 4);
			if (len < 0 || p + 12 + len > bytes.Length)
			{
				throw new InvalidDataException("the PNG picture is cut short");
			}
			ReadOnlySpan<byte> data = bytes.AsSpan(p + 8, len);
			switch (type)
			{
				case "IHDR":
					width = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
					height = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];
					depth = data[8];
					color = data[9];
					if (data[12] != 0)
					{
						throw new InvalidDataException("interlaced PNG pictures are not supported; save it without interlacing");
					}
					break;
				case "tEXt":
					int zero = data.IndexOf((byte)0);
					if (zero > 0)
					{
						text[Encoding.Latin1.GetString(data[..zero])] = Encoding.Latin1.GetString(data[(zero + 1)..]);
					}
					break;
				case "IDAT":
					idat.Write(data);
					break;
			}
			p += 12 + len;
			if (type == "IEND")
			{
				break;
			}
		}
		int channels = color switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => throw new InvalidDataException("palette PNG pictures are not supported; save it as grayscale or RGB") };
		if (depth != 8 && depth != 16)
		{
			throw new InvalidDataException($"{depth}-bit PNG pictures are not supported (8 or 16 bits)");
		}
		if (width <= 0 || height <= 0 || (long)width * height > 64L << 20)
		{
			throw new InvalidDataException("the picture is empty or too large");
		}
		int bpp = channels * depth / 8, stride = width * bpp;
		byte[] raw;
		using (ZLibStream zs = new(new MemoryStream(idat.ToArray()), CompressionMode.Decompress))
		using (MemoryStream o = new())
		{
			zs.CopyTo(o);
			raw = o.ToArray();
		}
		if (raw.Length < (stride + 1) * height)
		{
			throw new InvalidDataException("the PNG picture data is cut short");
		}
		float[] values = new float[width * height];
		byte[] prev = new byte[stride], row = new byte[stride];
		float max = depth == 16 ? 65535f : 255f;
		for (int y = 0; y < height; y++)
		{
			int o = y * (stride + 1), filter = raw[o];
			for (int i = 0; i < stride; i++)
			{
				int a = i >= bpp ? row[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
				row[i] = (byte)(raw[o + 1 + i] + filter switch { 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a, b, c), 0 => 0, _ => throw new InvalidDataException("unknown PNG row filter") });
			}
			for (int x = 0; x < width; x++)
			{
				float Sample(int ch) => depth == 16 ? ((row[x * bpp + ch * 2] << 8) | row[x * bpp + ch * 2 + 1]) / max : row[x * bpp + ch] / max;
				// Gray as is; colour as brightness; alpha (when there is one) is ignored.
				values[y * width + x] = channels >= 3 ? 0.299f * Sample(0) + 0.587f * Sample(1) + 0.114f * Sample(2) : Sample(0);
			}
			(prev, row) = (row, prev);
		}
		return new Image(width, height, values, text);
	}

	private static int Paeth(int a, int b, int c)
	{
		int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
		return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
	}

	private static void BigEndian(byte[] b, int o, int v)
	{
		b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
	}

	private static void Chunk(Stream s, string type, byte[] data)
	{
		byte[] len = new byte[4];
		BigEndian(len, 0, data.Length);
		s.Write(len);
		byte[] t = Encoding.ASCII.GetBytes(type);
		s.Write(t);
		s.Write(data);
		uint crc = Crc(Crc(0xFFFFFFFF, t), data) ^ 0xFFFFFFFF;
		byte[] c = new byte[4];
		BigEndian(c, 0, (int)crc);
		s.Write(c);
	}

	private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
	{
		uint c = (uint)n;
		for (int k = 0; k < 8; k++)
		{
			c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
		}
		return c;
	}).ToArray();

	private static uint Crc(uint crc, byte[] data)
	{
		foreach (byte b in data)
		{
			crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
		}
		return crc;
	}
}
