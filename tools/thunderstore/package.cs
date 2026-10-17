// The plugin package's helpers for tools/thunderstore.sh (a .NET file-based app: dotnet run package.cs).
//   changelog <CHANGELOG.md> <out.md> <version> [released tags...]
//       The plugin's changelog for its mod pages (Thunderstore, Hexium: the same text for both), made
//       from the editor's CHANGELOG.md (the editor and the plugin share one version and one changelog).
//       Each version is split in two: what changed in the plugin (the "### WorldEditorBridge" part of
//       the version, or "unchanged") and what changed in the editor, with a link to that editor on
//       GitHub (mod sites do not host programs). Only versioned sections are kept, newest first, as many
//       as fit in Thunderstore's 100,000 characters; the older history is in the full changelog on GitHub.
//   check <folder>
//       The package folder follows Thunderstore's rules: name a-z A-Z 0-9 _, description up to 250
//       characters, a Major.Minor.Patch version, dependencies as Team-Name-1.2.3, a 256x256 PNG icon
//       and a README.md and CHANGELOG.md in UTF-8, each up to 100,000 characters.
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string Repo = "https://github.com/Shuiei/valheim-world-editor";

// The heading of the plugin's part of a version in CHANGELOG.md.
const string PluginHeading = "### WorldEditorBridge";

// The most characters Thunderstore takes in a package's README.md or CHANGELOG.md.
const int MaxMarkdown = 100_000;

try
{
    switch (args.FirstOrDefault())
    {
        case "changelog" when args.Length >= 4:
            File.WriteAllText(args[2], Changelog(File.ReadAllText(args[1]), args[3], [.. args[4..]]), new UTF8Encoding(false));
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
    var lines = ChangelogHeader(version);
    string older = $"Older history: [the full changelog]({Repo}/blob/main/CHANGELOG.md).";
    int length = Length(lines) + Length([older]);
    // The version's own tag counts as released too, so its download link is always there.
    HashSet<string> released = [.. tags, $"v{version}"];
    foreach (string sec in Regex.Split(text, "^(?=## )", RegexOptions.Multiline))
    {
        var m = Regex.Match(sec, @"^## v([0-9]+\.[0-9]+\.[0-9]+)\b(.*)");
        if (!m.Success)
        {
            continue;
        }
        List<string> block = VersionBlock(sec, m.Groups[1].Value, m.Groups[2].Value, released);
        // Versions are newest first: once one does not fit, the rest is left to the full changelog.
        length += Length(block);
        if (length > MaxMarkdown)
        {
            break;
        }
        lines.AddRange(block);
    }
    lines.Add(older);
    return string.Join("\n", lines).TrimEnd('\n') + "\n";
}

// The first lines of the mod pages' changelog, before its versions.
static List<string> ChangelogHeader(string version) =>
[
    "# Changelog", "",
    "WorldEditorBridge is the live-editing plugin of **Valheim World Editor**: the two share one version.",
    "**Download the editor that goes with this plugin from GitHub:**",
    $"[Valheim World Editor v{version}]({Repo}/releases/tag/v{version}) (every version: the",
    $"[releases page]({Repo}/releases)). This mod page only hosts the plugin.", "",
    "Each version below says what changed in this plugin, then what changed in the editor.", "",
];

// The lines of one version of the changelog: v and title come from its "## vX.Y.Z title" heading, sec is its whole
// section, and released holds the tags (vX.Y.Z) that have a download link.
static List<string> VersionBlock(string sec, string v, string title, HashSet<string> released)
{
    // The version's parts ("### Added", "### WorldEditorBridge"...), split apart by whether they are the plugin's part.
    string[] parts = Regex.Split(sec.Split('\n', 2).ElementAtOrDefault(1) ?? "", "^(?=### )", RegexOptions.Multiline);
    var byPlugin = parts.Where(p => p.StartsWith("### ", StringComparison.Ordinal)).ToLookup(p => p.StartsWith(PluginHeading, StringComparison.Ordinal));
    // A version without a plugin part says so; one without an editor part reads "No change.".
    string pluginText = byPlugin[true].Select(p => p[(p.IndexOf('\n') + 1)..].Trim('\n')).DefaultIfEmpty("Unchanged: only the version number follows the editor's.").First();
    string editorText = string.Join("\n\n", byPlugin[false].Select(p => ("#" + p).Trim('\n')).DefaultIfEmpty("No change."));
    List<string> block =
    [
        $"## {v}{title}", "", "### WorldEditorBridge (this plugin)", "", pluginText,
        "", "### Valheim World Editor", "",
    ];
    if (released.Contains($"v{v}"))
    {
        block.AddRange([$"Download: [Valheim World Editor v{v}]({Repo}/releases/tag/v{v})", ""]);
    }
    block.Add(editorText);
    block.Add("");
    return block;
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
    // Width and height, 256 each, as big-endian 32-bit numbers at bytes 16 to 23 of the IHDR chunk.
    ReadOnlySpan<byte> size256 = [0, 0, 1, 0, 0, 0, 1, 0];
    Require(png.Length >= 24 && png.AsSpan(0, 8).SequenceEqual(signature) && png.AsSpan(16, 8).SequenceEqual(size256), "icon must be a 256x256 PNG");
    foreach (string name in new[] { "README.md", "CHANGELOG.md" })
    {
        string markdown;
        try
        {
            markdown = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(File.ReadAllBytes(Path.Combine(dir, name)));
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException($"{name} is not UTF-8");
        }
        Require(markdown.EnumerateRunes().Count() <= MaxMarkdown, $"{name} is longer than {MaxMarkdown} characters");
    }
}

// The characters lines take once joined with line breaks, as the mod sites count them (one per Unicode character).
static int Length(IEnumerable<string> lines) => lines.Sum(l => l.EnumerateRunes().Count() + 1);

// Throws InvalidDataException with what when ok is false.
static void Require(bool ok, string what)
{
    if (!ok)
    {
        throw new InvalidDataException(what);
    }
}
