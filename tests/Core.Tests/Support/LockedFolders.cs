using System.Runtime.Versioning;

namespace WorldEditor.Tests;

/// <summary>
/// A folder that cannot be deleted by a user other than root: its permissions take away the writing that deleting its
/// file needs. Made for the tests of a clean-up that must fail.
/// </summary>
[UnsupportedOSPlatform("windows")]
public static class LockedFolders
{
    /// <summary>
    /// A folder in <paramref name="parent"/> holding a file, made read and run only (the file cannot be deleted).
    /// Undo it with <see cref="Unlock"/>.
    /// </summary>
    public static string Make(string parent)
    {
        string locked = Path.Combine(parent, "locked");
        Directory.CreateDirectory(locked);
        File.WriteAllText(Path.Combine(locked, "held"), "");
        File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        return locked;
    }

    /// <summary>
    /// The folder made by <see cref="Make"/> made writable again, so that it can be deleted.
    /// </summary>
    public static void Unlock(string locked) =>
        File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
