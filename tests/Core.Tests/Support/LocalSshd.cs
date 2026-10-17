using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A real SSH server for the tunnel's tests: OpenSSH's sshd run as the test's own user on a free local port, with its
/// own host key, its own authorized keys and its own settings in a temporary folder (never ~/.ssh, never port 22).
/// Without sshd the tests skip, unless VWE_NEED_SSHD=1 (CI) makes that a failure.
/// </summary>
public sealed class LocalSshd : IDisposable
{
    private readonly Process? _p;
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "vwe-sshd-" + Guid.NewGuid().ToString("N")[..8]);
    public int Port { get; }
    public string User { get; } = Environment.UserName;
    public string? Why { get; }
    public string Fingerprint { get; } = "";

    // A key the server lets in, and one it does not know.
    public string Key => Path.Combine(Dir, "user");
    public string OtherKey => Path.Combine(Dir, "other");

    public LocalSshd()
    {
        string? sshd = new[] { "/usr/sbin/sshd", "/usr/bin/sshd", "/sbin/sshd" }.FirstOrDefault(File.Exists);
        string? keygen = new[] { "/usr/bin/ssh-keygen", "/usr/sbin/ssh-keygen", "/bin/ssh-keygen" }.FirstOrDefault(File.Exists);
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() || sshd == null || keygen == null)
        {
            Why = "OpenSSH's sshd and ssh-keygen are needed";
            return;
        }
        Directory.CreateDirectory(Dir);
        foreach (string k in new[] { "host", "user", "other" })
        {
            Exec(keygen, "-q", "-t", "ed25519", "-N", "", "-C", "vwe-test", "-f", Path.Combine(Dir, k));
        }
        File.Copy(Path.Combine(Dir, "user.pub"), Path.Combine(Dir, "authorized_keys"));
        // "256 SHA256:... comment (ED25519)"
        Fingerprint = Exec(keygen, "-l", "-f", Path.Combine(Dir, "host.pub")).Split(' ')[1];
        Port = FakeGame.FreePort();
        File.WriteAllText(Path.Combine(Dir, "sshd_config"), $"""
			Port {Port}
			ListenAddress 127.0.0.1
			HostKey {Path.Combine(Dir, "host")}
			AuthorizedKeysFile {Path.Combine(Dir, "authorized_keys")}
			PidFile {Path.Combine(Dir, "sshd.pid")}
			UsePAM no
			StrictModes no
			PubkeyAuthentication yes
			PasswordAuthentication no
			KbdInteractiveAuthentication no
			PermitRootLogin prohibit-password
			AllowTcpForwarding yes
			LogLevel ERROR
			""");
        var psi = new ProcessStartInfo(sshd) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string a in new[] { "-D", "-e", "-f", Path.Combine(Dir, "sshd_config") })
        {
            psi.ArgumentList.Add(a);
        }
        _p = Process.Start(psi)!;
        var log = new StringBuilder();
        _p.ErrorDataReceived += (_, e) => { lock (log) { log.AppendLine(e.Data); } };
        _p.BeginErrorReadLine();
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (_p.HasExited)
            {
                break;
            }
            try
            {
                using var c = new TcpClient();
                c.Connect(IPAddress.Loopback, Port);
                return;
            }
            catch (SocketException)
            {
                Thread.Sleep(100);
            }
        }
        lock (log)
        {
            Why = "sshd did not start: " + log.ToString().Trim();
        }
        Dispose();
        _p = null;
    }

    public bool Available => _p != null;

    /// <summary>
    /// Skips the test without sshd (fails instead when CI asks for it).
    /// </summary>
    public void Need()
    {
        if (!Available && Environment.GetEnvironmentVariable("VWE_NEED_SSHD") == "1")
        {
            Assert.Fail(Why);
        }
        Assert.SkipUnless(Available, Why ?? "");
    }

    /// <summary>
    /// Runs a program with these arguments and returns what it wrote to standard output.
    /// </summary>
    private static string Exec(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string a in args)
        {
            psi.ArgumentList.Add(a);
        }
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return output;
    }

    public void Dispose()
    {
        try
        {
            if (_p is { HasExited: false })
            {
                _p.Kill();
                _p.WaitForExit(5000);
            }
        }
        catch
        {
            // The process may have exited already; the test is over either way.
        }
        try
        {
            Directory.Delete(Dir, true);
        }
        catch
        {
            // Best effort: a folder that cannot be deleted is left for the OS to clean up.
        }
    }
}
