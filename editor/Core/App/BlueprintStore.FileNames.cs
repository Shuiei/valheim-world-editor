namespace TerrainEditor.App;

/// <summary>
/// The file names of <see cref="BlueprintStore"/>: how a blueprint's name becomes its id, and which ids are safe.
/// </summary>
public sealed partial class BlueprintStore
{
    /// <summary>
    /// A file name from the blueprint's name: letters, digits, spaces, dashes, underscores and dots are kept. Other
    /// characters become "_", leading and trailing dots and spaces are dropped (after the cut too: Windows drops a
    /// trailing dot itself); at most 80 characters, "blueprint" when nothing is left, and "_" before a name Windows
    /// keeps for a device (CON, NUL…).
    /// </summary>
    public static string IdFor(string name)
    {
        string id = Clean(name);
        if (id.Length == 0)
        {
            id = "blueprint";
        }
        // Cut again after the "_": an id is always one Clean keeps as it is (ValidId), so it can be read back.
        return SafeFile.IsDeviceName(id) ? Clean("_" + id) : id;
    }

    /// <summary>
    /// The name's characters a file name keeps (others "_"), without dots and spaces at its ends, at most 80.
    /// </summary>
    private static string Clean(string name)
    {
        string id = new string([.. name.Trim().Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.' ? c : '_')]).Trim('.', ' ');
        return (id.Length > 80 ? id[..80] : id).TrimEnd('.', ' ');
    }

    /// <summary>
    /// An id <c>Clean</c> leaves as it is and holds no folder part, so it cannot point outside the folder. A device
    /// name saved before such names were changed still counts: its file is there to read or delete.
    /// </summary>
    private static bool ValidId(string id) => id.Length > 0 && id == Clean(id) && Path.GetFileName(id) == id;
}
