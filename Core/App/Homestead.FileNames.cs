using System.Buffers;

namespace TerrainEditor.App;

/// <summary>
/// File names for blueprints: a blueprint's name made into a file name that Windows and Linux both keep, and a
/// name not taken yet among those already in the folder.
/// </summary>
public static partial class Homestead
{
    /// <summary>Characters a blueprint's file name keeps before ".blueprint".</summary>
    private const int FileNameLength = 64;

    /// <summary>The characters a file name cannot hold on Windows or Linux.</summary>
    private static readonly SearchValues<char> NotInFileNames = SearchValues.Create([.. Path.GetInvalidFileNameChars(), '<', '>', ':', '"', '/', '\\', '|', '?', '*']);

    /// <summary>
    /// A file name for a blueprint name: what Windows and Linux both allow. Forbidden characters become _, dots and
    /// spaces at the ends are dropped (after the cut too: Windows drops a trailing dot itself, and the picture's name
    /// would no longer match), at most 64 characters before ".blueprint", "blueprint" when nothing is left, and "_"
    /// before a name Windows keeps for a device (CON, NUL…).
    /// </summary>
    public static string FileName(string name)
    {
        string f = new string([.. name.Trim().Select(c => NotInFileNames.Contains(c) ? '_' : c)]).Trim('.', ' ');
        f = (f.Length > FileNameLength ? f[..FileNameLength] : f).TrimEnd('.', ' ');
        if (f.Length == 0)
        {
            f = "blueprint";
        }
        return (SafeFile.IsDeviceName(f) ? "_" + f : f) + ".blueprint";
    }

    /// <summary>
    /// The name itself when <paramref name="taken"/> says it is free, else the first "name (n)" (n from 2) that is.
    /// A long name is cut so that the number survives <see cref="FileName"/>'s 64-character cut: without that, every
    /// numbered name of a 64-character blueprint would give the same file and the search would never end.
    /// </summary>
    /// <param name="name">The wanted name.</param>
    /// <param name="taken">Whether a blueprint of that name exists already.</param>
    public static string FreeName(string name, Func<string, bool> taken)
    {
        name = name.Trim();
        if (!taken(name))
        {
            return name;
        }
        for (int n = 2; ; n++)
        {
            string suffix = $" ({n})";
            string stem = name.Length + suffix.Length > FileNameLength ? name[..(FileNameLength - suffix.Length)].TrimEnd() : name;
            string numbered = stem + suffix;
            if (!taken(numbered))
            {
                return numbered;
            }
        }
    }
}
