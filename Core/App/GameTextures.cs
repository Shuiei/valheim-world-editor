using BCnEncoder.Decoder;
using BCnEncoder.Shared;

namespace TerrainEditor.App;

// The game's textures as pixels: one mip level (GameBundles.LoadTexture) made RGBA, rows bottom first
// as Unity keeps them. Block-compressed ones (DXT1, DXT5, BC7...) are decoded with BCnEncoder.Net.
public static class GameTextures
{
	// RGBA bytes of a level, or null for a format not read here.
	public static byte[]? Rgba(GameBundles.TextureLevel t)
	{
		int n = t.Width * t.Height;
		var rgba = new byte[n * 4];
		switch (t.Format)
		{
			case 10 or 12 or 25 or 26 or 27:
				var format = t.Format switch
				{
					10 => CompressionFormat.Bc1,
					12 => CompressionFormat.Bc3,
					25 => CompressionFormat.Bc7,
					26 => CompressionFormat.Bc4,
					_ => CompressionFormat.Bc5,
				};
				var px = new BcDecoder().DecodeRaw(t.Data, t.Width, t.Height, format);
				for (int i = 0; i < n; i++)
				{
					rgba[i * 4] = px[i].r;
					rgba[i * 4 + 1] = px[i].g;
					rgba[i * 4 + 2] = px[i].b;
					rgba[i * 4 + 3] = t.Format == 10 ? (byte)255 : px[i].a;
				}
				return rgba;
			case 4:
				Buffer.BlockCopy(t.Data, 0, rgba, 0, n * 4);
				return rgba;
			case 5:
				for (int i = 0; i < n; i++)
				{
					rgba[i * 4] = t.Data[i * 4 + 1];
					rgba[i * 4 + 1] = t.Data[i * 4 + 2];
					rgba[i * 4 + 2] = t.Data[i * 4 + 3];
					rgba[i * 4 + 3] = t.Data[i * 4];
				}
				return rgba;
			case 14:
				for (int i = 0; i < n; i++)
				{
					rgba[i * 4] = t.Data[i * 4 + 2];
					rgba[i * 4 + 1] = t.Data[i * 4 + 1];
					rgba[i * 4 + 2] = t.Data[i * 4];
					rgba[i * 4 + 3] = t.Data[i * 4 + 3];
				}
				return rgba;
			case 3:
				for (int i = 0; i < n; i++)
				{
					rgba[i * 4] = t.Data[i * 3];
					rgba[i * 4 + 1] = t.Data[i * 3 + 1];
					rgba[i * 4 + 2] = t.Data[i * 3 + 2];
					rgba[i * 4 + 3] = 255;
				}
				return rgba;
			case 1 or 63:
				for (int i = 0; i < n; i++)
				{
					byte v = t.Data[i];
					rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = t.Format == 1 ? (byte)255 : v;
					rgba[i * 4 + 3] = t.Format == 1 ? v : (byte)255;
				}
				return rgba;
			default:
				return null;
		}
	}

	// Cut-out textures: transparent pixels take the colour of nearby opaque ones (up to 16 pixels away,
	// wrapping around: textures tile), the rest the average opaque colour, so that filtering and mipmaps
	// do not bleed the hidden colour (often beige) into leaf edges. Alpha is kept.
	public static void Bleed(byte[] rgba, int w, int h)
	{
		int n = w * h;
		var filled = new bool[n];
		int opaque = 0;
		long sr = 0, sg = 0, sb = 0;
		for (int i = 0; i < n; i++)
		{
			if (rgba[i * 4 + 3] >= 128)
			{
				filled[i] = true;
				opaque++;
				sr += rgba[i * 4];
				sg += rgba[i * 4 + 1];
				sb += rgba[i * 4 + 2];
			}
		}
		if (opaque == 0 || opaque == n)
		{
			return;
		}
		var next = new List<(int I, byte R, byte G, byte B)>();
		for (int it = 0; it < 16; it++)
		{
			next.Clear();
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					int i = y * w + x;
					if (filled[i])
					{
						continue;
					}
					int c = 0, r = 0, g = 0, b = 0;
					for (int dy = -1; dy <= 1; dy++)
					{
						for (int dx = -1; dx <= 1; dx++)
						{
							int j = ((y + dy + h) % h) * w + (x + dx + w) % w;
							if (filled[j])
							{
								c++;
								r += rgba[j * 4];
								g += rgba[j * 4 + 1];
								b += rgba[j * 4 + 2];
							}
						}
					}
					if (c > 0)
					{
						next.Add((i, (byte)((r + c / 2) / c), (byte)((g + c / 2) / c), (byte)((b + c / 2) / c)));
					}
				}
			}
			if (next.Count == 0)
			{
				break;
			}
			foreach (var (i, r, g, b) in next)
			{
				rgba[i * 4] = r;
				rgba[i * 4 + 1] = g;
				rgba[i * 4 + 2] = b;
				filled[i] = true;
			}
		}
		byte mr = (byte)(sr / opaque), mg = (byte)(sg / opaque), mb = (byte)(sb / opaque);
		for (int i = 0; i < n; i++)
		{
			if (!filled[i])
			{
				rgba[i * 4] = mr;
				rgba[i * 4 + 1] = mg;
				rgba[i * 4 + 2] = mb;
			}
		}
	}
}
