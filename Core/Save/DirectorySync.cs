using System.Runtime.InteropServices;

namespace TerrainEditor.Save;

/// <summary>
/// Flushes a directory's entries to disk. On Linux and macOS a flushed file's name is only durable once its directory is
/// flushed too (fsync(2)), and .NET cannot open a directory as a file there, so libc does it. Windows needs none: NTFS
/// journals the names.
/// </summary>
internal static partial class DirectorySync
{
    /// <summary>EINVAL (the same on Linux and macOS): the file system cannot flush a directory; nothing more to do there.</summary>
    private const int Einval = 22;

    /// <summary>Flushes the names in this directory to disk (the files created in it, renamed or removed).</summary>
    /// <exception cref="IOException">The directory could not be opened or flushed.</exception>
    public static void Flush(string dir)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        int fd = Open(dir, 0);
        if (fd < 0)
        {
            throw new IOException($"Could not open {dir} to flush it (errno {Marshal.GetLastPInvokeError()}).");
        }
        try
        {
            if (Fsync(fd) != 0 && Marshal.GetLastPInvokeError() is int errno and not Einval)
            {
                throw new IOException($"Could not flush {dir} to disk (errno {errno}).");
            }
        }
        finally
        {
            _ = Close(fd);
        }
    }

    /// <summary>open(2), read only.</summary>
    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags);

    /// <summary>fsync(2).</summary>
    [LibraryImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static partial int Fsync(int fd);

    /// <summary>close(2).</summary>
    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int Close(int fd);
}
