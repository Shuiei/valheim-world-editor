// The plugin package's helpers for tools/thunderstore.sh (a .NET file-based app: dotnet run package.cs).
//   changelog <CHANGELOG.md> <notes folder> <out.md> <version>
//       The plugin's changelog for its mod pages (Thunderstore, Hexium: the same text for both): one part
//       for each release, with its notes as its GitHub release has them (the editor and the plugin share one
//       version and one release), from the notes folder (v<version>.md each, made by tools/release/publish.cs
//       notes-all), dated as CHANGELOG.md's heading of that version, with a link to that editor on GitHub (mod
//       sites do not host programs). The notes' Downloads part (the release page's files) is left out and
//       their links to the repository's files made whole. Newest first, as many releases as fit in
//       Thunderstore's 100,000 characters; the older history is in the full changelog on GitHub.
//   check <folder>
//       The package folder follows Thunderstore's rules: name a-z A-Z 0-9 _, description up to 250
//       characters, a Major.Minor.Patch version, dependencies as Team-Name-1.2.3, a 256x256 PNG icon
//       and a README.md and CHANGELOG.md in UTF-8, each up to 100,000 characters.
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string Repo = "https://github.com/Shuiei/valheim-world-editor";

// The most characters Thunderstore takes in a package's README.md or CHANGELOG.md.
const int MaxMarkdown = 100_000;

// How long a regular expression may run on the changelog's text before it stops with an error (milliseconds).
const int RegexTimeout = 1000;

try
{
    switch (args.FirstOrDefault())
    {
        case "changelog" when args.Length == 5:
            File.WriteAllText(args[3], Changelog(File.ReadAllText(args[1]), args[2], args[4]), new UTF8Encoding(false));
            return 0;
        case "check" when args.Length == 2:
            Check(args[1]);
            return 0;
        default:
            Console.Error.WriteLine("usage: package.cs changelog <CHANGELOG.md> <notes folder> <out.md> <version> | check <folder>");
            return 2;
    }
}
catch (InvalidDataException ex)
{
    Console.Error.WriteLine($"{args.ElementAtOrDefault(1)}: {ex.Message}");
    return 1;
}

// The changelog command: the mod pages' changelog, one part for each release whose notes are in notesDir (v1.2.3.md),
// newest first, dated as text's (CHANGELOG.md's) heading of that version. version's notes must be there.
static string Changelog(string text, string notesDir, string version)
{
    var lines = ChangelogHeader(version);
    string older = $"Older history: [the full changelog]({Repo}/blob/main/CHANGELOG.md).";
    int length = Length(lines) + Length([older]);
    var releases = Directory.GetFiles(notesDir, "v*.md")
        .Select(f => (File: f, Version: Version.Parse(Path.GetFileNameWithoutExtension(f)[1..])))
        .OrderByDescending(r => r.Version).ToList();
    if (!releases.Any(r => r.Version.ToString(3) == version))
    {
        throw new InvalidDataException($"no release notes for v{version} (v{version}.md)");
    }

    foreach (var (file, v) in releases)
    {
        string number = v.ToString(3);
        // The heading's title after the version: its date, and a word on the version for the first ones.
        string title = Regex.Match(text, $@"^## v{Regex.Escape(number)}\b(.*)$", RegexOptions.Multiline, TimeSpan.FromMilliseconds(RegexTimeout)).Groups[1].Value.TrimEnd();
        List<string> block =
        [
            $"## {number}{title}", "",
            $"Download the editor: [Valheim World Editor v{number}]({Repo}/releases/tag/v{number})", "",
            ReleaseNotes(File.ReadAllText(file)), "",
        ];
        // Releases are newest first: once one does not fit, the rest is left to the full changelog.
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

// The first lines of the mod pages' changelog, before its releases.
static List<string> ChangelogHeader(string version) =>
[
    "# Changelog", "",
    "WorldEditorBridge is the live-editing plugin of **Valheim World Editor**: the two share one version.",
    "**Download the editor that goes with this plugin from GitHub:**",
    $"[Valheim World Editor v{version}]({Repo}/releases/tag/v{version}) (every version: the",
    $"[releases page]({Repo}/releases)). This mod page only hosts the plugin.", "",
    "Each release below as its GitHub release page tells it: what it brings to the editor, and under",
    "**WorldEditorBridge** what changed in this plugin (a release without that part left the plugin as it was).", "",
];

// One release's notes for the changelog: without their Downloads part (the release page's files), their headings one
// level down under the release's own, and their links to the repository's files made whole (the mod pages are
// elsewhere).
static string ReleaseNotes(string notes)
{
    var kept = new List<string>();
    bool downloads = false, code = false;
    foreach (string line in notes.Replace("\r\n", "\n").Split('\n'))
    {
        if (line.StartsWith("```", StringComparison.Ordinal))
        {
            code = !code;
        }

        if (!code && line.StartsWith("## ", StringComparison.Ordinal))
        {
            downloads = line.TrimEnd() == "## Downloads";
        }

        if (downloads)
        {
            continue;
        }

        kept.Add(code ? line : Regex.Replace(line, "^(#{1,5}) ", "#$1 ", RegexOptions.None, TimeSpan.FromMilliseconds(RegexTimeout)));
    }

    string text = string.Join("\n", kept).Trim('\n');
    // A link to a file of the repository (docs/README.md, not https:, # or mailto:) goes to it on GitHub.
    return Regex.Replace(text, @"\]\((?![a-z]+:|#)([^)\s]+)\)", m => $"]({Repo}/blob/main/{m.Groups[1].Value})",
        RegexOptions.None, TimeSpan.FromMilliseconds(RegexTimeout));
}

// The check command: throws InvalidDataException naming the first rule the package folder breaks (a missing
// file or manifest key throws the runtime's own exception instead).
static void Check(string dir)
{
    using var m = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")));
    var root = m.RootElement;
    Require(Regex.IsMatch(root.GetProperty("name").GetString()!, "^[A-Za-z0-9_]{1,128}$", RegexOptions.None, TimeSpan.FromMilliseconds(RegexTimeout)), "name");
    Require(root.GetProperty("description").GetString()!.EnumerateRunes().Count() <= 250, "description too long");
    Require(Regex.IsMatch(root.GetProperty("version_number").GetString()!, @"^[0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.None,
        TimeSpan.FromMilliseconds(RegexTimeout)), "version");
    Require(root.GetProperty("dependencies").EnumerateArray().All(d => Regex.IsMatch(d.GetString()!, @"^\w+-\w+-[0-9]+\.[0-9]+\.[0-9]+$",
        RegexOptions.None, TimeSpan.FromMilliseconds(RegexTimeout))), "dependencies");
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
