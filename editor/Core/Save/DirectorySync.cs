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
    /// <summary>fcntl's F_FULLFSYNC on macOS (sys/fcntl.h).</summary>
    private const int FullFsync = 51;

    /// <summary>
    /// Flushes the names in this directory to disk (the files created in it, renamed or removed). Every failure is an
    /// error, a file system that cannot flush a directory too: what was written there is not known to be on disk.
    /// </summary>
    /// <exception cref="IOException">The directory could not be opened, flushed or closed.</exception>
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
        int flushed = OperatingSystem.IsMacOS() ? Fcntl(fd, FullFsync) : Fsync(fd);
        int flushError = flushed != 0 ? Marshal.GetLastPInvokeError() : 0;
        int closed = Close(fd);
        int closeError = closed != 0 ? Marshal.GetLastPInvokeError() : 0;
        if (flushed != 0)
        {
            throw new IOException($"Could not flush {dir} to disk (errno {flushError}).");
        }
        if (closed != 0)
        {
            throw new IOException($"Could not close {dir} after flushing it (errno {closeError}).");
        }
    }

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
