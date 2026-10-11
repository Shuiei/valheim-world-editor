// The plugin package's helpers for tools/thunderstore.sh (a .NET file-based app: dotnet run package.cs).
//   changelog <CHANGELOG.md> <out.md> <version> [released tags...]
//       The plugin's changelog for its mod pages (Thunderstore, Hexium: the same text for both), made
//       from the editor's CHANGELOG.md (the editor and the plugin share one version and one changelog).
//       Each version is split in two: what changed in the plugin (the "### WorldEditorBridge" part of
//       the version, or "unchanged") and what changed in the editor, with a link to that editor on
//       GitHub (mod sites do not host programs). Only versioned sections are kept; the older history is
//       in the full changelog on GitHub.
//   check <folder>
//       The package folder follows Thunderstore's rules: name a-z A-Z 0-9 _, description up to 250
//       characters, a Major.Minor.Patch version, dependencies as Team-Name-1.2.3, a 256x256 PNG icon
//       and a README.md in UTF-8.
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string Repo = "https://github.com/Shuiei/valheim-world-editor";

try
{
    switch (args.FirstOrDefault())
    {
        case "changelog" when args.Length >= 4:
            File.WriteAllText(args[2], Changelog(File.ReadAllText(args[1]), args[3], args[4..].ToHashSet()), new UTF8Encoding(false));
            return 0;
        case "check" when args.Length == 2:
            Check(args[1]);
            return 0;
        default:
            Console.Error.WriteLine("usage: package.cs changelog <CHANGELOG.md> <out.md> <version> [tags...] | check <folder>");
            return 2;
    }
}
catch (InvalidDataException ex)
{
    Console.Error.WriteLine($"{args.ElementAtOrDefault(1)}: {ex.Message}");
    return 1;
}

// The changelog command: the mod pages' changelog from CHANGELOG.md's text. version gets a download link, and so
// does each older version whose tag (v1.2.3) is in tags.
static string Changelog(string text, string version, HashSet<string> tags)
{
    var lines = new List<string>
    {
        "# Changelog", "",
        "WorldEditorBridge is the live-editing plugin of **Valheim World Editor**: the two share one version.",
        "**Download the editor that goes with this plugin from GitHub:**",
        $"[Valheim World Editor v{version}]({Repo}/releases/tag/v{version}) (every version: the",
        $"[releases page]({Repo}/releases)). This mod page only hosts the plugin.", "",
        "Each version below says what changed in this plugin, then what changed in the editor.", "",
    };
    foreach (string sec in Regex.Split(text, "^(?=## )", RegexOptions.Multiline))
    {
        var m = Regex.Match(sec, @"^## v([0-9]+\.[0-9]+\.[0-9]+)\b(.*)");
        if (!m.Success)
            continue;
        string v = m.Groups[1].Value, title = m.Groups[2].Value;
        // The version's parts ("### Added", "### WorldEditorBridge"...).
        int nl = sec.IndexOf('\n');
        string[] parts = Regex.Split(nl >= 0 ? sec[(nl + 1)..] : "", "^(?=### )", RegexOptions.Multiline);
        var plugin = parts.Where(p => p.StartsWith("### WorldEditorBridge", StringComparison.Ordinal)).ToList();
        var editor = parts.Where(p => p.StartsWith("### ", StringComparison.Ordinal) && !p.StartsWith("### WorldEditorBridge", StringComparison.Ordinal)).ToList();
        lines.AddRange([$"## {v}{title}", "", "### WorldEditorBridge (this plugin)", ""]);
        lines.Add(plugin.Count > 0 ? plugin[0][(plugin[0].IndexOf('\n') + 1)..].Trim('\n') : "Unchanged: only the version number follows the editor's.");
        lines.AddRange(["", "### Valheim World Editor", ""]);
        if (v == version || tags.Contains($"v{v}"))
            lines.AddRange([$"Download: [Valheim World Editor v{v}]({Repo}/releases/tag/v{v})", ""]);
        lines.Add(editor.Count > 0 ? string.Join("\n\n", editor.Select(p => ("#" + p).Trim('\n'))) : "No change.");
        lines.Add("");
    }
    lines.Add($"Older history: [the full changelog]({Repo}/blob/main/CHANGELOG.md).");
    return string.Join("\n", lines).TrimEnd('\n') + "\n";
}

// The check command: throws InvalidDataException naming the first rule the package folder breaks (a missing
// file or manifest key throws the runtime's own exception instead).
static void Check(string dir)
{
    using var m = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")));
    var root = m.RootElement;
    Require(Regex.IsMatch(root.GetProperty("name").GetString()!, "^[A-Za-z0-9_]{1,128}$"), "name");
    Require(root.GetProperty("description").GetString()!.EnumerateRunes().Count() <= 250, "description too long");
    Require(Regex.IsMatch(root.GetProperty("version_number").GetString()!, @"^[0-9]+\.[0-9]+\.[0-9]+$"), "version");
    Require(root.GetProperty("dependencies").EnumerateArray().All(d => Regex.IsMatch(d.GetString()!, @"^\w+-\w+-[0-9]+\.[0-9]+\.[0-9]+$")), "dependencies");
    byte[] png = File.ReadAllBytes(Path.Combine(dir, "icon.png"));
    ReadOnlySpan<byte> signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    Require(png.Length >= 24 && png.AsSpan(0, 8).SequenceEqual(signature)
        && BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16)) == 256 && BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20)) == 256, "icon must be a 256x256 PNG");
    try
    {
        new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(File.ReadAllBytes(Path.Combine(dir, "README.md")));
    }
    catch (DecoderFallbackException)
    {
        throw new InvalidDataException("README.md is not UTF-8");
    }
}

// Throws InvalidDataException with what when ok is false.
static void Require(bool ok, string what)
{
    if (!ok)
        throw new InvalidDataException(what);
}
