using System.Runtime.InteropServices;

namespace TerrainEditor.Save;

/// <summary>
/// Flushes a directory's entries to disk. On Linux and macOS a flushed file's name is only durable once its directory is
/// flushed too (fsync(2)), and .NET cannot open a directory as a file there, so libc does it. On macOS with F_FULLFSYNC,
/// which also has the drive write out its cache, as .NET's FileStream.Flush(true) does there. Windows needs none: NTFS
/// journals the names.
/// </summary>
internal static partial class DirectorySync
{
    /// <summary>EINVAL (the same on Linux and macOS).</summary>
    private const int Einval = 22;

    /// <summary>EROFS (the same on Linux and macOS).</summary>
    private const int Erofs = 30;

    /// <summary>fcntl's F_FULLFSYNC on macOS (sys/fcntl.h).</summary>
    private const int FullFsync = 51;

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
            int done = OperatingSystem.IsMacOS() ? Fcntl(fd, FullFsync) : Fsync(fd);
            if (done != 0 && Marshal.GetLastPInvokeError() is int errno && !CannotFlush(errno))
            {
                throw new IOException($"Could not flush {dir} to disk (errno {errno}).");
            }
        }
        finally
        {
            _ = Close(fd);
        }
    }

    /// <summary>
    /// The file system has no flush for it (EINVAL, ENOTSUP, EROFS): nothing more can be done there, and .NET's
    /// FileStream.Flush(true) passes over the same errors for the files themselves.
    /// </summary>
    private static bool CannotFlush(int errno) => errno is Einval or Erofs || errno == (OperatingSystem.IsMacOS() ? 45 : 95);

    /// <summary>open(2), read only.</summary>
    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags);

    /// <summary>fsync(2).</summary>
    [LibraryImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static partial int Fsync(int fd);

    /// <summary>fcntl(2), with a command that takes no argument.</summary>
    [LibraryImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static partial int Fcntl(int fd, int cmd);

    /// <summary>close(2).</summary>
    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int Close(int fd);
}
