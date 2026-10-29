namespace TerrainEditor.App;

/// <summary>
/// Saved dedicated servers, in servers.cfg in the app's data folder: one [section] per server with
/// "Key = value" lines, like BepInEx configs, so it can also be edited by hand. Written after the
/// first successful connection; the password only when "Save password" was ticked (plain text, in a
/// file only this user can read on Linux).
/// </summary>
public static class ServerConfig
{
    /// <summary>One saved server: how to reach it over SSH and its bridge plugin.</summary>
    public sealed class Server
    {
        /// <summary>The name shown for it (its [section]); the host when none was given.</summary>
        public string Name { get; set; } = "";

        /// <summary>The SSH host name or address.</summary>
        public string Host { get; set; } = "";

        /// <summary>The SSH port.</summary>
        public int SshPort { get; set; } = 22;

        /// <summary>The SSH user.</summary>
        public string User { get; set; } = "";

        /// <summary>The SSH password, only when "Save password" was ticked (plain text).</summary>
        public string? Password { get; set; }

        /// <summary>The SSH private key file, if one is used.</summary>
        public string? KeyFile { get; set; }

        /// <summary>The bridge plugin's token, if known.</summary>
        public string? Token { get; set; }

        /// <summary>The bridge plugin's port on the server.</summary>
        public int BridgePort { get; set; } = 5182;

        /// <summary>
        /// The server's Valheim folder (with BepInEx), optional: for the plugin's port and precise errors.
        /// </summary>
        public string? GameFolder { get; set; }

        /// <summary>The server's SSH host key, remembered on first connection: a different key is refused.</summary>
        public string? HostKey { get; set; }

        /// <summary>user@host:port, what tells saved servers apart.</summary>
        public string Id => $"{User}@{Host}:{SshPort}";
    }

    /// <summary>Tests: another file, so they never touch the user's servers.</summary>
    public static string? PathOverride { get; set; }

    /// <summary>The servers file: servers.cfg in the data folder, unless overridden.</summary>
    public static string FilePath => PathOverride ?? Path.Combine(AppSettings.DataDir, "servers.cfg");

    /// <summary>Keeps reads and writes of the file from overlapping.</summary>
    private static readonly Lock Lock = new();

    /// <summary>
    /// The saved servers, in the file's order (most recent first); those without a host or user are left
    /// out. Unknown keys and lines are ignored; empty when there is no file.
    /// </summary>
    public static List<Server> Load()
    {
        lock (Lock)
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }
            List<Server> list = ServerConfigFile.Parse(File.ReadAllLines(FilePath));
            foreach (Server s in list)
            {
                // Kept out of the log, whatever fails later.
                Log.Private(s.Host, s.User, s.KeyFile, s.GameFolder);
                Log.Secret(s.Password, s.Token);
            }
            return [.. list.Where(s => s.Host.Length > 0 && s.User.Length > 0)];
        }
    }

    /// <summary>
    /// Adds or updates a server (matched by user, host and SSH port), most recent first. With no name it
    /// keeps the saved one's, else takes the host.
    /// </summary>
    /// <exception cref="ArgumentException">A value (other than the name) holds a line break.</exception>
    public static void Remember(Server server)
    {
        lock (Lock)
        {
            List<Server> list = Load();
            Server? old = list.FirstOrDefault(s => s.Id == server.Id);
            if (old != null)
            {
                list.Remove(old);
                if (string.IsNullOrEmpty(server.Name))
                {
                    server.Name = old.Name;
                }
            }
            if (string.IsNullOrEmpty(server.Name))
            {
                server.Name = server.Host;
            }
            list.Insert(0, server);
            Write(list);
        }
    }

    /// <summary>Removes the saved server with this <see cref="Server.Id"/>.</summary>
    public static void Forget(string id)
    {
        lock (Lock)
        {
            Write([.. Load().Where(s => s.Id != id)]);
        }
    }

    /// <summary>
    /// The server's [section] name as written in the file (a ']' would end it early, a line break start another line).
    /// </summary>
    public static string SectionName(Server s) => s.Name.Replace("]", ")").ReplaceLineEndings(" ");

    /// <summary>True when the value fits on one line of the file: a line break would start a line of its own.</summary>
    public static bool OneLine(string? value) => value == null || value.AsSpan().IndexOfAny('\r', '\n') < 0;

    /// <summary>True for a TCP port number (1 to 65535).</summary>
    public static bool IsPort(int port) => port is > 0 and <= 65535;

    /// <summary>Writes the whole file, readable by this user only.</summary>
    /// <exception cref="ArgumentException">A value (other than the name) holds a line break.</exception>
    private static void Write(List<Server> list)
    {
        // Only the user can read it, from the start (it can hold a password).
        SafeFile.WriteAllText(FilePath, ServerConfigFile.Format(list), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
