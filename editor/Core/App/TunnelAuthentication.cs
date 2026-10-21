using System.Security.Cryptography;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace TerrainEditor.App;

/// <summary>
/// How a tunnel logs in: the chosen SSH key file, else the usual key files on this machine, and the password
/// (also answering keyboard-interactive prompts). The key files read are kept by the caller to be closed.
/// </summary>
internal static class TunnelAuthentication
{
    /// <summary>Builds the login methods to try, in order, reading the key files.</summary>
    /// <param name="r">What the player entered.</param>
    /// <param name="user">The account to log in with.</param>
    /// <param name="keys">Where the key files read are kept, for the caller to close.</param>
    /// <returns>The methods, and the message for the player when the chosen key file cannot be read or nothing can log in (null otherwise).</returns>
    internal static (List<AuthenticationMethod> Methods, string? Error) Methods(Tunnel.Request r, string user, List<PrivateKeyFile> keys)
    {
        var methods = new List<AuthenticationMethod>();
        try
        {
            if (!string.IsNullOrWhiteSpace(r.KeyPath))
            {
                string key = Places.ExpandHome(r.KeyPath);
                keys.Add(string.IsNullOrEmpty(r.Passphrase) ? new PrivateKeyFile(key) : new PrivateKeyFile(key, r.Passphrase));
                methods.Add(new PrivateKeyAuthenticationMethod(user, keys[^1]));
            }
            else
            {
                // No key given: the usual key files, if any (no passphrase).
                foreach (string name in new[] { "id_ed25519", "id_ecdsa", "id_rsa" })
                {
                    string k = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", name);
                    if (File.Exists(k))
                    {
                        // One that cannot be used (a passphrase, a format SSH.NET does not read, no
                        // access) is skipped, and logged: the password is still tried.
                        try
                        {
                            keys.Add(new PrivateKeyFile(k));
                            methods.Add(new PrivateKeyAuthenticationMethod(user, keys[^1]));
                        }
                        catch (Exception ex) when (KeyUnreadable(ex))
                        {
                            Log.CaughtBrief(ex, $"reading the SSH key {Path.GetFileName(k)} (skipped: the password is still tried)");
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (KeyUnreadable(ex))
        {
            Log.Caught(ex, "reading the chosen SSH key file (not connected)");
            return (methods, "The key file could not be read: " + ex.Message);
        }
        if (!string.IsNullOrEmpty(r.Password))
        {
            methods.Add(new PasswordAuthenticationMethod(user, r.Password));
            var kbd = new KeyboardInteractiveAuthenticationMethod(user);
            kbd.AuthenticationPrompt += (_, e) => { foreach (var p in e.Prompts) { p.Response = r.Password; } };
            methods.Add(kbd);
        }
        if (methods.Count == 0)
        {
            return (methods, "Enter the password, or choose your SSH key file.");
        }
        return (methods, null);
    }

    /// <summary>
    /// Whether reading a key file failed because of the file: missing, no access, a wrong passphrase, or a format
    /// SSH.NET does not read (it reports those in many ways, such as an unsupported DSA key or a key cut short).
    /// </summary>
    /// <param name="ex">What reading the key threw.</param>
    /// <returns>True for the failures that come from the key file.</returns>
    internal static bool KeyUnreadable(Exception ex) => ex is SshException or IOException or UnauthorizedAccessException or InvalidOperationException
        or ArgumentException or NotSupportedException or FormatException or System.Formats.Asn1.AsnContentException or CryptographicException;
}
