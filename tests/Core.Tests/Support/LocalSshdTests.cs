using System.Net;
using System.Net.Sockets;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The tests' SSH server (LocalSshd) disposed: sshd and the session processes it started end, and its folder goes; a
/// folder that cannot be deleted fails the disposal.
/// </summary>
public sealed class LocalSshdTests
{
    /// <summary>The processes whose parent is this one (from /proc).</summary>
    private static int[] Children(int parent) => [.. Directory.GetDirectories("/proc")
        .Select(Path.GetFileName)
        .Where(d => d!.All(char.IsAsciiDigit))
        .Select(d => int.Parse(d!, System.Globalization.CultureInfo.InvariantCulture))
        .Where(p => Stat(p) is { } stat && stat.Parent == parent)];

    /// <summary>A process's state letter and parent (from /proc/pid/stat), or null once it is gone.</summary>
    private static (char State, int Parent)? Stat(int pid)
    {
        string text;
        try
        {
            text = File.ReadAllText($"/proc/{pid}/stat");
        }
        catch (IOException) when (!Directory.Exists($"/proc/{pid}"))
        {
            // Ended (and reaped) meanwhile.
            return null;
        }
        // "pid (name) state ppid ...": the name may hold spaces and brackets, so read after its last ')'.
        string[] rest = text[(text.LastIndexOf(')') + 2)..].Split(' ');
        return (rest[0][0], int.Parse(rest[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Whether a process has ended: gone, or a zombie left for a parent that does not reap it.</summary>
    private static bool Ended(int pid) => Stat(pid) is not { } stat || stat.State == 'Z';

    [Fact]
    public void DisposingEndsSshdAndItsSessionsAndDeletesItsFolder()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the processes are found in /proc");
        var ssh = new LocalSshd();
        if (!ssh.Available)
        {
            ssh.Dispose();
            ssh.Need();
        }
        int sshd = ssh.ProcessId;
        // A connection held open: sshd starts a process of its own for it.
        using var connection = new TcpClient();
        connection.Connect(IPAddress.Loopback, ssh.Port);
        int[] sessions = [];
        Assert.True(SpinWait.SpinUntil(() => (sessions = Children(sshd)).Length > 0, TimeSpan.FromSeconds(30)), "sshd started no process for the connection");

        ssh.Dispose();

        Assert.True(Ended(sshd));
        Assert.True(SpinWait.SpinUntil(() => sessions.All(Ended), TimeSpan.FromSeconds(30)), "a session of sshd's did not end");
        Assert.False(Directory.Exists(ssh.Dir));
    }

    [Fact]
    public void AFolderThatCannotBeDeletedFailsTheDisposal()
    {
        // Root deletes whatever the folders' permissions (CI's containers run as root).
        if (!OperatingSystem.IsLinux() || Environment.UserName == "root")
        {
            Assert.Skip("only a user other than root is refused a folder by its permissions");
            return;
        }
        var ssh = new LocalSshd();
        if (!ssh.Available)
        {
            ssh.Dispose();
            ssh.Need();
        }
        string locked = LockedFolders.Make(ssh.Dir);
        try
        {
            var e = Record.Exception(ssh.Dispose);

            Assert.True(e is UnauthorizedAccessException or IOException, $"the disposal did not fail: {e}");
        }
        finally
        {
            LockedFolders.Unlock(locked);
            ssh.Dispose();
        }
    }
}
