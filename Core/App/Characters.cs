using System.Text;

namespace TerrainEditor.App;

/// <summary>
/// The player's own Valheim characters (.fch files on this computer): name and player id, the id the
/// game writes as "creator" on everything the character builds.
/// </summary>
public static class Characters
{
    /// <summary>
    /// One character.
    /// </summary>
    /// <param name="Name">The character's name as stored in its profile.</param>
    /// <param name="Id">The player id the game writes as "creator".</param>
    public sealed record Character(string Name, long Id);

    /// <summary>
    /// Character folders next to the world folders (characters_local and characters).
    /// </summary>
    public static IEnumerable<string> Roots() => Places.WorldRoots()
        .Select(r => Path.Combine(Path.GetDirectoryName(r)!, Path.GetFileName(r) == "worlds_local" ? "characters_local" : "characters"))
        .Distinct();

    /// <summary>
    /// This computer's characters, each player id once (the first file by name wins); files that cannot be read are
    /// passed over.
    /// </summary>
    public static List<Character> Local()
    {
        List<Character> list = [];
        foreach (string root in Roots().Where(Directory.Exists))
        {
            // In name order, not the file system's (it differs between file systems): of a character
            // and its backup copy, the copy's longer name comes second, and the original is kept.
            foreach (string f in Directory.GetFiles(root, "*.fch").OrderBy(f => f, StringComparer.Ordinal))
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

    /// <summary>
    /// The name and player id from a character file's bytes. The profile stores the name (a length-prefixed
    /// string), then the player id (8 bytes), then the start seed (a short string), after the per-world data.
    /// The file is named after the character, so the first place the name appears followed by an id and a seed
    /// is it.
    /// </summary>
    /// <param name="b">The .fch file's bytes.</param>
    /// <param name="fileName">The file name without extension: the character's name, matched ignoring case (as
    /// the game names the file).</param>
    /// <returns>The character, or null when no name followed by a plausible id and seed is found.</returns>
    public static Character? Read(byte[] b, string fileName)
    {
        int length = Encoding.UTF8.GetByteCount(fileName);
        if (length is 0 or > 127)
        {
            return null;
        }
        // The name as text, compared ignoring case: lowering the bytes covers only ASCII ("Ørjan" never matched).
        Span<char> name = stackalloc char[length];
        for (int i = 1; i + length + 9 <= b.Length; i++)
        {
            if (b[i - 1] != length || !Matches(b.AsSpan(i, length), name, fileName))
            {
                continue;
            }
            int j = i + length;
            long id = BitConverter.ToInt64(b, j);
            int seedLength = b[j + 8];
            if (id == 0 || seedLength > 32 || j + 9 + seedLength > b.Length || !b.AsSpan(j + 9, seedLength).ToArray().All(c => c is >= 32 and < 127))
            {
                continue;
            }
            return new Character(Encoding.UTF8.GetString(b, i, length), id);
        }
        return null;
    }

    /// <summary>Whether UTF-8 bytes say <paramref name="wanted"/>, ignoring case.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="chars">Room for their characters (as many as bytes).</param>
    /// <param name="wanted">The name.</param>
    private static bool Matches(ReadOnlySpan<byte> bytes, Span<char> chars, string wanted)
    {
        int n = Encoding.UTF8.GetChars(bytes, chars);
        return chars[..n].Equals(wanted, StringComparison.OrdinalIgnoreCase);
    }
}
