using System.Text.RegularExpressions;

namespace TerrainEditor.App;

/// <summary>
/// Taking personal data and secrets out of the log: the values to keep out, the scrubbing rules and their patterns.
/// </summary>
public static partial class Log
{
    /// <summary>
    /// Keeps values out of the log from now on (a server's address, an account name, a password, a token):
    /// <see cref="Scrub"/> writes &lt;private&gt; in their place. Empty values and those shorter than three characters are
    /// left out, as they would take letters out of every word, and so is this computer's own address (localhost,
    /// 127.x.x.x, ::1): it is no one's personal data, and <see cref="Scrub"/> keeps it on purpose.
    /// </summary>
    /// <param name="values">The values; nulls are left out.</param>
    public static void Private(params ReadOnlySpan<string?> values)
    {
        lock (PrivateLock)
        {
            foreach (string? v in values)
            {
                if (v != null && v.Trim().Length >= 3 && !IsThisComputer(v.Trim()))
                {
                    PrivateValues.Add(v);
                }
            }
        }
    }

    /// <summary>
    /// Keeps secrets out of the log from now on (passwords, passphrases, tokens), whatever their length: unlike
    /// <see cref="Private"/>, one shorter than three characters is kept out too, where it stands alone (not inside a
    /// word, which it would otherwise take letters out of: a one-letter token would empty every line).
    /// </summary>
    /// <param name="values">The secrets; nulls, empty and blank ones are left out (a blank one would take every space out of the log).</param>
    public static void Secret(params ReadOnlySpan<string?> values)
    {
        lock (PrivateLock)
        {
            foreach (string? v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    PrivateValues.Add(v);
                }
            }
        }
    }

    /// <summary>
    /// Text with personal data taken out, for the log: the values given to <see cref="Private"/> and
    /// <see cref="Secret"/>, web addresses,
    /// user@host names, file paths (a stack trace's source files keep only their name; a path with spaces that is not
    /// in quotes loses only its first part), IP addresses other than the local one, host names before a port, and the
    /// account's user name.
    /// </summary>
    public static string Scrub(string text)
    {
        string[] secrets;
        lock (PrivateLock)
        {
            secrets = [.. PrivateValues.OrderByDescending(v => v.Length)];
        }
        // Whole values first, before the rules below cut one up (a password holding an @ or a /).
        foreach (string v in secrets.Where(v => v.Length >= 3))
        {
            text = text.Replace(v, "<private>", StringComparison.Ordinal);
        }
        text = SourceFile().Replace(text, m => ") in " + LastPart(m.Groups[1].Value) + ":line ");
        text = WebAddress().Replace(text, "<url>");
        text = UserAtHost().Replace(text, "<user@host>");
        text = WithoutHome(text);
        text = QuotedPath().Replace(text, m => m.Groups[1].Value + "<path>" + m.Groups[1].Value);
        text = BarePath().Replace(text, m => "<path>" + m.Value[m.Value.TrimEnd('.', ':').Length..]);
        text = Ipv4().Replace(text, m => m.Value.StartsWith("127.", StringComparison.Ordinal) ? m.Value : "<ip>");
        text = Ipv6().Replace(text, m => m.Value == "[::1]" ? m.Value : "[<ip>]");
        text = HostPort().Replace(text, m => m.Groups[1].Value.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? m.Value : "(<host>:" + m.Groups[2].Value + ")");
        // Short secrets last, where they stand alone in what is left (not a letter of a path already taken out).
        foreach (string v in secrets.Where(v => v.Length < 3))
        {
            text = ReplaceWhole(text, v, "<private>", wordStart: true);
        }
        string user = Environment.UserName;
        return user.Length >= 3 ? ReplaceWhole(text, user, "<user>", wordStart: true) : text;
    }

    /// <summary>Values that must never reach the log: see <see cref="Private"/> and <see cref="Secret"/>.</summary>
    private static readonly HashSet<string> PrivateValues = new(StringComparer.Ordinal);

    /// <summary>Guards <see cref="PrivateValues"/>.</summary>
    private static readonly Lock PrivateLock = new();
    /// <summary>Whether an address is this computer's own: localhost, 127.x.x.x or ::1.</summary>
    private static bool IsThisComputer(string address) =>
        address.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || address.StartsWith("127.", StringComparison.Ordinal)
        || address is "::1" or "[::1]";

    /// <summary>The last part of a path, on either system's separators.</summary>
    private static string LastPart(string path) => path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
    /// <summary>A stack frame's source file, in ") in /path/File.cs:line 12"; group 1 is the path.</summary>
    [GeneratedRegex(@"\) in ([^\r\n]+?):line (?=\d)")]
    private static partial Regex SourceFile();

    /// <summary>A web address (scheme://…).</summary>
    [GeneratedRegex(@"\b[A-Za-z][A-Za-z0-9+.\-]*://[^\s'""<>()]+")]
    private static partial Regex WebAddress();

    /// <summary>user@host, or an email address.</summary>
    [GeneratedRegex(@"[\w.+\-]+@[\w\-]+(?:\.[\w\-]+)*")]
    private static partial Regex UserAtHost();

    /// <summary>A path in quotes (as .NET's file errors give them); group 1 is the quote.</summary>
    [GeneratedRegex(@"(['""])(?:~(?:[\\/][^'""\r\n]*)?|[A-Za-z]:[\\/][^'""\r\n]*|\\\\[^'""\r\n]+|/[^'""\r\n]+)\1")]
    private static partial Regex QuotedPath();

    /// <summary>A path not in quotes, to the first space; not one inside a word (and/or, 1/2) or already replaced.</summary>
    [GeneratedRegex(@"(?<![\w.~:/\\<>\-])(?:~(?!\w)|[A-Za-z]:[\\/]|\\\\(?=\w)|/(?=[\w.~\-]))[^\s'""<>|,;()\[\]]*")]
    private static partial Regex BarePath();

    /// <summary>An IPv4 address (not part of a version such as Version=1.0.0.0).</summary>
    [GeneratedRegex(@"(?<![\w.=])(?:\d{1,3}\.){3}\d{1,3}(?![\w.])")]
    private static partial Regex Ipv4();

    /// <summary>An IPv6 address in brackets.</summary>
    [GeneratedRegex(@"\[(?=[0-9A-Fa-f.]*:)[0-9A-Fa-f:.]+\]")]
    private static partial Regex Ipv6();

    /// <summary>"(host:port)", as socket errors end; group 1 is the host, 2 the port.</summary>
    [GeneratedRegex(@"\(([A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*):(\d{1,5})\)")]
    private static partial Regex HostPort();
}
