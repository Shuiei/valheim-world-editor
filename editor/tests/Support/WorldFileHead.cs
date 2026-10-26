using TerrainEditor.App;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>A world file's head (_main.&lt;n&gt;.fwl2) written as the game writes it, for the tests to read back.</summary>
public static class WorldFileHead
{
    /// <summary>The test world's name, seed and generator version in the head written.</summary>
    public const string Name = "CITest", SeedName = "GbXfypqU3G";

    /// <summary>
    /// Writes a world file as the game does: the version, names, seed, uid, generator version, database flag (30 and
    /// later), keys (32 and later), players (none). With a generator's id, a world generation mod's marker and that id
    /// first (nothing more of a header: for a generator none draws).
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="version">The game's file version.</param>
    /// <param name="keys">The starting global keys.</param>
    /// <param name="generator">The generator's id written after the marker; null for a world the game made.</param>
    public static void Write(string path, int version, string[] keys, string? generator = null)
    {
        using MemoryStream pkg = new();
        using (BinaryWriter p = new(pkg, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            if (generator != null)
            {
                p.Write(WorldGenerators.Marker);
                p.Write(generator);
            }
            p.Write(version);
            p.Write(Name);
            p.Write(SeedName);
            p.Write(StableHash.Of(SeedName));
            p.Write(123456789L);
            p.Write(2);
            if (version >= 30)
            {
                p.Write(true);
            }
            if (version >= 32)
            {
                p.Write(keys.Length);
                foreach (string k in keys)
                {
                    p.Write(k);
                }
            }
            p.Write(0);
        }
        File.WriteAllBytes(path, [.. BitConverter.GetBytes((int)pkg.Length), .. pkg.ToArray()]);
    }
}
