using System.Text;

namespace TerrainEditor.App;

// The player's own Valheim characters (.fch files on this computer): name and player id, the id the
// game writes as "creator" on everything the character builds.
public static class Characters
{
	public sealed record Character(string Name, long Id);

	// Character folders next to the world folders (characters_local and characters).
	public static IEnumerable<string> Roots() => Places.WorldRoots()
		.Select(r => Path.Combine(Path.GetDirectoryName(r)!, Path.GetFileName(r) == "worlds_local" ? "characters_local" : "characters"))
		.Distinct();

	public static List<Character> Local()
	{
		List<Character> list = new();
		foreach (string root in Roots().Where(Directory.Exists))
		{
			foreach (string f in Directory.GetFiles(root, "*.fch"))
			{
				try
				{
					if (Read(File.ReadAllBytes(f), Path.GetFileNameWithoutExtension(f)) is { } c && !list.Any(x => x.Id == c.Id))
					{
						list.Add(c);
					}
				}
				// A file that cannot be read (another user's, locked) is passed over.
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
				}
			}
		}
		return list;
	}

	// The profile stores the name (a length-prefixed string), then the player id (8 bytes), then the
	// start seed (a short string), after the per-world data. The file is named after the character, so
	// the first place the name appears followed by an id and a seed is it.
	public static Character? Read(byte[] b, string fileName)
	{
		byte[] want = Encoding.UTF8.GetBytes(fileName.ToLowerInvariant());
		if (want.Length is 0 or > 127)
		{
			return null;
		}
		for (int i = 1; i + want.Length + 9 <= b.Length; i++)
		{
			if (b[i - 1] != want.Length || !Matches(b, i, want))
			{
				continue;
			}
			int j = i + want.Length;
			long id = BitConverter.ToInt64(b, j);
			int seedLength = b[j + 8];
			if (id == 0 || seedLength > 32 || j + 9 + seedLength > b.Length || !b.AsSpan(j + 9, seedLength).ToArray().All(c => c is >= 32 and < 127))
			{
				continue;
			}
			return new Character(Encoding.UTF8.GetString(b, i, want.Length), id);
		}
		return null;
	}

	private static bool Matches(byte[] b, int at, byte[] lower)
	{
		for (int k = 0; k < lower.Length; k++)
		{
			byte c = b[at + k];
			if ((c is >= (byte)'A' and <= (byte)'Z' ? (byte)(c + 32) : c) != lower[k])
			{
				return false;
			}
		}
		return true;
	}
}
