// A release's steps that talk to GitHub's release notes and the mod sites, for CircleCI's release
// workflow (.circleci/continue.yml; a .NET file-based app: dotnet run tools/release/publish.cs -- ...).
// Run it from the repository's root: the sites' settings are in tools/release/sites.json.
//   notes <CHANGELOG.md> <version> <last tag, or -> <out.md> [<notes.md>]
//       The release notes: notes.md (docs/releases/v<version>.md) when it is there, else the CHANGELOG's
//       sections newer than the last released tag, up to version.
//   thunderstore|hexium|nexus <dist> [--notes <notes.md>] [--dry-run]
//       Publishes the packages in dist to one site: WorldEditorBridge-<version>.zip (made by
//       tools/thunderstore.sh; the version is its manifest's) and, for Nexus Mods, the editor's
//       ValheimWorldEditor-v<version>-linux-x64.tar.gz and -win-x64.zip. --dry-run checks all it can
//       without publishing: the token, that the version is not on the site yet, the categories, the
//       files. A version already on the site fails: no site takes a version twice. --notes (Nexus
//       Mods): the release notes, added to the mod's changelog.
// The tokens come from the environment: THUNDERSTORE_TOKEN, HEXIUM_TOKEN, NEXUS_API_KEY.
#:property PublishAot=false
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const string Repo = "https://github.com/Shuiei/valheim-world-editor";
const string SitesFile = "tools/release/sites.json";

try
{
    switch (args.FirstOrDefault())
    {
        case "notes" when args.Length is 5 or 6:
            File.WriteAllText(args[4], Notes(File.ReadAllText(args[1]), args[2], args[3], args.ElementAtOrDefault(5)), new UTF8Encoding(false));
            return 0;
        case "thunderstore" or "hexium" or "nexus" when args.Length >= 2:
            string[] options = args[2..];
            bool dryRun = options.Contains("--dry-run");
            int notesAt = Array.IndexOf(options, "--notes");
            string? notes = notesAt >= 0 && notesAt + 1 < options.Length ? File.ReadAllText(options[notesAt + 1]) : null;
            var site = JsonNode.Parse(File.ReadAllText(SitesFile))![args[0]]!;
            var release = FindRelease(args[1], args[0] == "nexus");
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd($"valheim-world-editor-release/{release.Version}");
                switch (args[0])
                {
                    case "thunderstore":
                        await PublishThunderstore(http, site, release, dryRun);
                        break;
                    case "hexium":
                        await PublishHexium(http, site, release, dryRun);
                        break;
                    default:
                        await PublishNexus(http, site, release, notes, dryRun);
                        break;
                }
            }

            return 0;
        default:
            Console.Error.WriteLine("usage: publish.cs notes <CHANGELOG.md> <version> <last tag|-> <out.md> [notes.md]");
            Console.Error.WriteLine("       publish.cs thunderstore|hexium|nexus <dist> [--notes <notes.md>] [--dry-run]");
            return 2;
    }
}
catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or HttpRequestException or IOException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

// The notes command: notesFile's text when it exists, else CHANGELOG.md's sections after lastTag (v1.2.3, or "-"
// for none) up to version, newest first, as they are.
static string Notes(string changelog, string version, string lastTag, string? notesFile)
{
    if (notesFile is not null && File.Exists(notesFile))
    {
        return File.ReadAllText(notesFile);
    }

    var current = Version.Parse(version);
    Version? last = lastTag == "-" ? null : Version.Parse(lastTag.TrimStart('v'));
    var kept = new List<string>();
    foreach (string section in Regex.Split(changelog, "^(?=## )", RegexOptions.Multiline))
    {
        var m = Regex.Match(section, @"^## v([0-9]+\.[0-9]+\.[0-9]+)\b");
        if (!m.Success)
        {
            continue;
        }

        var v = Version.Parse(m.Groups[1].Value);
        if (v <= current && (last is null || v > last))
        {
            kept.Add(section.Trim());
        }
    }

    if (kept.Count == 0)
    {
        throw new InvalidDataException($"CHANGELOG.md has no section for v{version}.");
    }

    return string.Join("\n\n", kept) + $"\n\nFull list: [CHANGELOG.md]({Repo}/blob/main/CHANGELOG.md).\n";
}

// The release's packages in dist. The editor's are needed only for Nexus Mods (the other sites take the plugin only).
static Release FindRelease(string dist, bool editorToo)
{
    string[] bridges = Directory.GetFiles(dist, "WorldEditorBridge-*.zip");
    if (bridges.Length != 1)
    {
        throw new InvalidDataException($"{dist} must hold one WorldEditorBridge-<version>.zip (it holds {bridges.Length}).");
    }

    JsonNode manifest;
    byte[] manifestBytes, icon;
    using (var zip = ZipFile.OpenRead(bridges[0]))
    {
        manifestBytes = ReadEntry(zip, "manifest.json");
        icon = ReadEntry(zip, "icon.png");
        manifest = JsonNode.Parse(manifestBytes)!;
    }

    string version = (string)manifest["version_number"]!;
    var release = new Release(version, (string)manifest["name"]!, bridges[0], manifestBytes, icon,
        Path.Combine(dist, $"ValheimWorldEditor-v{version}-linux-x64.tar.gz"), Path.Combine(dist, $"ValheimWorldEditor-v{version}-win-x64.zip"));
    if (editorToo)
    {
        foreach (string file in new[] { release.Linux, release.Windows }.Where(f => !File.Exists(f)))
        {
            throw new InvalidDataException($"Missing {file}: the editor's packages must be those of version {version}.");
        }
    }

    Console.WriteLine($"{release.Name} {version}: {bridges[0]}");
    return release;
}

static byte[] ReadEntry(ZipArchive zip, string name)
{
    var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"The plugin zip has no {name}.");
    using var stream = entry.Open();
    using var copy = new MemoryStream();
    stream.CopyTo(copy);
    return copy.ToArray();
}

// ---- Thunderstore and Hexium (Hexium follows Thunderstore's API).

static async Task PublishThunderstore(HttpClient http, JsonNode site, Release release, bool dryRun)
{
    const string Name = "Thunderstore";
    string url = Text(site, "url"), team = Text(site, "team"), community = Text(site, "community");
    string[] categories = Texts(site, "categories");
    string token = Token("THUNDERSTORE_TOKEN");
    await NotThereYet(http, Name, $"{url}/api/experimental/package/{team}/{release.Name}/{release.Version}/");
    var user = await Call(http, Name, Get($"{url}/api/experimental/current-user/", Bearer(token)));
    if (user?["teams"]?.AsArray().Any(t => (string?)t == team) != true)
    {
        throw new InvalidOperationException($"{Name}: the token is not a service account of team {team}.");
    }

    await CategoriesExist(http, Name, $"{url}/api/experimental/community/{community}/category/", "slug", categories);
    // Thunderstore's own checks of the manifest and icon (its README check is broken on its side: always 500;
    // tools/thunderstore/package.cs checks the README by the same rules).
    var checks = new (string What, string Path, JsonObject Body)[]
    {
        ("manifest", "manifest-v1", new JsonObject { ["namespace"] = team, ["manifest_data"] = Convert.ToBase64String(release.Manifest) }),
        ("icon", "icon", new JsonObject { ["icon_data"] = Convert.ToBase64String(release.Icon) }),
    };
    foreach (var (what, path, body) in checks)
    {
        try
        {
            var answer = await Call(http, Name, Post($"{url}/api/experimental/submission/validate/{path}/", body, Bearer(token)));
            if (answer?["success"]?.GetValue<bool>() != true)
            {
                throw new InvalidOperationException($"{Name} refuses the package's {what}: {answer?.ToJsonString()}");
            }
        }
        catch (SiteException ex) when (ex.Status >= 500)
        {
            Console.WriteLine($"warning: {Name}'s {what} check is not working right now ({ex.Status}); not a verdict on the package.");
        }
    }

    Console.WriteLine($"{Name}: {team}-{release.Name} {release.Version} can be published (categories {string.Join(", ", categories)}).");
    if (dryRun)
    {
        return;
    }

    string upload = await Upload(http, Name, url, token, release.Bridge, tokenToParts: false);
    var result = await Submit(http, Name, url, token, upload, team, community, categories);
    Console.WriteLine($"{Name}: published {result?["package_version"]?["full_name"]}, {url}/c/{community}/p/{team}/{release.Name}/");
}

static async Task PublishHexium(HttpClient http, JsonNode site, Release release, bool dryRun)
{
    const string Name = "Hexium";
    string url = Text(site, "url"), team = Text(site, "team"), community = Text(site, "community"), install = Text(site, "installLocation");
    string[] categories = Texts(site, "categories");
    string token = Token("HEXIUM_TOKEN");
    // Hidden versions (every upload until it is unhidden on hexium.gg) do not show here: the upload is refused then.
    await NotThereYet(http, Name, $"{url}/api/experimental/package/{team}/{release.Name}/{release.Version}/");
    await CategoriesExist(http, Name, $"{url}/api/experimental/community/{community}/category/", "name", categories);
    if (dryRun)
    {
        // Hexium has no endpoint that only checks a token: an upload begun and called off checks it.
        var begun = await Call(http, Name, Post($"{url}/api/experimental/usermedia/initiate-upload/",
            new JsonObject { ["filename"] = Path.GetFileName(release.Bridge), ["file_size_bytes"] = new FileInfo(release.Bridge).Length }, Bearer(token)));
        await Call(http, Name, Post($"{url}/api/experimental/usermedia/{begun!["user_media"]!["uuid"]}/abort-upload/", new JsonObject(), Bearer(token)));
        Console.WriteLine($"{Name}: {team}-{release.Name} {release.Version} can be published (install location {install}, categories {string.Join(", ", categories)}).");
        return;
    }

    string upload = await Upload(http, Name, url, token, release.Bridge, tokenToParts: true);
    JsonNode? result;
    try
    {
        result = await Submit(http, Name, url, token, upload, team, community, categories);
    }
    catch (SiteException ex) when (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"{ex.Message}\nThat version is on Hexium already, hidden: unhide it on its Versions page on hexium.gg.", ex);
    }

    Console.WriteLine($"{Name}: published {result?["package_version"]?["full_name"]} (hidden: unhide it on its Versions page on hexium.gg).");
    var meta = new JsonObject { ["install_location"] = install, ["categories"] = new JsonArray([.. categories.Select(c => JsonValue.Create(c))]) };
    await Call(http, Name, Post($"{url}/api/mod/{team}/{release.Name}/set-metadata", meta, Bearer(token)));
    Console.WriteLine($"{Name}: install location {install}, categories {string.Join(", ", categories)}.");
}

// The version must not be on the site: its page answers 404.
static async Task NotThereYet(HttpClient http, string site, string url)
{
    if (await Call(http, site, Get(url), missingIsNull: true) is not null)
    {
        throw new InvalidOperationException($"{site}: this version is published already ({url}). Raise VERSION: no site takes a version twice.");
    }
}

static async Task CategoriesExist(HttpClient http, string site, string url, string key, string[] wanted)
{
    var known = new HashSet<string>();
    for (string? page = url; page is not null;)
    {
        var answer = await Call(http, site, Get(page));
        foreach (var category in answer!["results"]!.AsArray())
        {
            known.Add((string)category![key]!);
        }

        page = (string?)(answer["pagination"]?["next_link"] ?? answer["next"]);
    }

    string[] missing = [.. wanted.Where(c => !known.Contains(c))];
    if (missing.Length > 0)
    {
        throw new InvalidOperationException($"{site} has no category {string.Join(", ", missing)} (tools/release/sites.json).");
    }
}

// Uploads the zip in the parts the site asks for and returns the upload's id; an upload that fails is called off.
// tokenToParts: the token goes with each part sent to the site itself (Hexium's parts may be its own endpoint);
// never to a storage URL.
static async Task<string> Upload(HttpClient http, string site, string url, string token, string zip, bool tokenToParts)
{
    var begun = await Call(http, site, Post($"{url}/api/experimental/usermedia/initiate-upload/",
        new JsonObject { ["filename"] = Path.GetFileName(zip), ["file_size_bytes"] = new FileInfo(zip).Length }, Bearer(token)));
    string uuid = (string)begun!["user_media"]!["uuid"]!;
    try
    {
        var parts = new JsonArray();
        using (var file = File.OpenRead(zip))
        {
            foreach (var part in begun["upload_urls"]!.AsArray())
            {
                byte[] data = new byte[(int)part!["length"]!];
                file.Seek((long)part["offset"]!, SeekOrigin.Begin);
                file.ReadExactly(data);
                var target = new Uri(new Uri(url), (string)part["url"]!);
                using var put = new HttpRequestMessage(HttpMethod.Put, target) { Content = new ByteArrayContent(data) };
                if (tokenToParts && target.Host == new Uri(url).Host)
                {
                    put.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                using var answer = await http.SendAsync(put);
                if (!answer.IsSuccessStatusCode)
                {
                    throw new SiteException($"{site}: uploading part {part["part_number"]} failed ({(int)answer.StatusCode}).", (int)answer.StatusCode);
                }

                string etag = answer.Headers.TryGetValues("ETag", out var tags) ? tags.First() : "";
                parts.Add(new JsonObject { ["ETag"] = etag, ["PartNumber"] = (int)part["part_number"]! });
            }
        }

        await Call(http, site, Post($"{url}/api/experimental/usermedia/{uuid}/finish-upload/", new JsonObject { ["parts"] = parts }, Bearer(token)));
        return uuid;
    }
    catch (Exception ex) when (ex is SiteException or HttpRequestException or IOException)
    {
        try
        {
            await Call(http, site, Post($"{url}/api/experimental/usermedia/{uuid}/abort-upload/", new JsonObject(), Bearer(token)));
        }
        catch (Exception abort) when (abort is SiteException or HttpRequestException)
        {
            Console.WriteLine($"warning: {site}: calling off the upload failed: {abort.Message}");
        }

        throw;
    }
}

// Submits an uploaded zip: the site checks and publishes it before answering.
static async Task<JsonNode?> Submit(HttpClient http, string site, string url, string token, string upload, string team, string community, string[] categories)
{
    var body = new JsonObject
    {
        ["upload_uuid"] = upload,
        ["author_name"] = team,
        ["communities"] = new JsonArray(community),
        ["community_categories"] = new JsonObject { [community] = new JsonArray([.. categories.Select(c => JsonValue.Create(c))]) },
        ["has_nsfw_content"] = false,
    };
    return await Call(http, site, Post($"{url}/api/experimental/submission/submit/", body, Bearer(token)));
}

// ---- Nexus Mods: a new version of each of the mod's existing files (v3 API, as Nexus-Mods/upload-action).

static async Task PublishNexus(HttpClient http, JsonNode site, Release release, string? notes, bool dryRun)
{
    const string Name = "Nexus Mods";
    string url = Text(site, "url"), game = Text(site, "game");
    int modId = (int)site["modId"]!;
    string key = Token("NEXUS_API_KEY");
    (string, string)[] auth = [("apikey", key), ("Application-Name", "valheim-world-editor-release"), ("Application-Version", release.Version)];
    var user = await Call(http, Name, Get($"{url}/v1/users/validate.json", auth));
    Console.WriteLine($"{Name}: the key is {user?["name"]}'s.");
    string modUid = (string)(await V3(http, Get($"{url}/v3/games/{game}/mods/{modId}", auth)))!["id"]!;

    string work = Directory.CreateTempSubdirectory("nexus-").FullName;
    var uploads = new List<(JsonNode File, string Archive)>();
    foreach (var file in site["files"]!.AsArray().OfType<JsonNode>())
    {
        string modFile = Text(file, "modFileId");
        var versions = (await V3(http, Get($"{url}/v3/mod-files/{modFile}/versions", auth)))!["versions"]!.AsArray();
        if (versions.Any(v => (string?)v!["version"] == release.Version && (string?)v["category"] is not ("removed" or "archived")))
        {
            // An earlier run that stopped part way published this one: the rerun does the rest.
            Console.WriteLine($"{Name}: file {modFile} has version {release.Version} already (an earlier run): skipped.");
            continue;
        }

        // Nexus takes .zip, .rar and .7z only; the plugin goes where Vortex and a hand install put it.
        string archive = Text(file, "package") switch
        {
            "bridge" => NexusPlugin(release.Bridge, release.Name, Path.Combine(work, Path.GetFileName(release.Bridge))),
            "linux" => TarToZip(release.Linux, Path.Combine(work, Path.GetFileName(release.Linux)[..^".tar.gz".Length] + ".zip")),
            "windows" => release.Windows,
            var other => throw new InvalidDataException($"tools/release/sites.json: unknown Nexus package {other}."),
        };
        uploads.Add((file, archive));
        Console.WriteLine($"{Name}: {Path.GetFileName(archive)} ({new FileInfo(archive).Length / 1024} KiB) → file {modFile}, \"{Template(file, "name", release)}\".");
    }

    if (uploads.Count == 0)
    {
        throw new InvalidOperationException($"{Name}: every file has a version {release.Version} already. Raise VERSION: no site takes a version twice.");
    }

    if (dryRun)
    {
        Console.WriteLine($"{Name}: mod {modId} can take version {release.Version} of its {uploads.Count} files.");
        return;
    }

    foreach (var (file, archive) in uploads)
    {
        string upload = await NexusUpload(http, url, auth, archive);
        var body = new JsonObject
        {
            ["upload_id"] = upload,
            ["name"] = Template(file, "name", release),
            ["description"] = Template(file, "description", release),
            ["version"] = release.Version,
            ["file_category"] = "main",
            ["archive_existing_file"] = true,
            ["update_mod_version"] = (bool)file["updateModVersion"]!,
            ["allow_mod_manager_download"] = (bool)file["allowModManagerDownload"]!,
        };
        await V3(http, Post($"{url}/v3/mod-files/{Text(file, "modFileId")}/versions", body, auth));
        Console.WriteLine($"{Name}: published {body["name"]}.");
    }

    if (notes is not null)
    {
        await V3(http, Post($"{url}/v3/mods/{modUid}/changelogs", new JsonObject { ["version"] = release.Version, ["changelog"] = PlainText(notes) }, auth));
        Console.WriteLine($"{Name}: changelog added.");
    }

    Console.WriteLine($"{Name}: https://www.nexusmods.com/{game}/mods/{modId}?tab=files");
}

// Uploads a file and waits until Nexus has processed it; returns the upload's id. Up to 100 MiB in one part: its
// presigned URL signs the Content-Type, the Content-Disposition (with the name given) and the Content-MD5, so the
// PUT sends exactly these. Bigger files go in the parts Nexus asks for.
static async Task<string> NexusUpload(HttpClient http, string url, (string, string)[] auth, string path)
{
    const long SinglePartLimit = 100 * 1024 * 1024;
    string name = Path.GetFileName(path);
    long size = new FileInfo(path).Length;
    JsonNode upload;
    if (size <= SinglePartLimit)
    {
        byte[] data = await File.ReadAllBytesAsync(path);
        // Nexus asks for the MD5 (a checksum here, not security).
#pragma warning disable CA5351
        byte[] md5 = MD5.HashData(data);
#pragma warning restore CA5351
        upload = (await V3(http, Post($"{url}/v3/uploads", new JsonObject { ["filename"] = name, ["size_bytes"] = size, ["md5"] = Convert.ToHexStringLower(md5) }, auth)))!;
        using var put = new HttpRequestMessage(HttpMethod.Put, (string)upload["presigned_url"]!) { Content = new ByteArrayContent(data) };
        put.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        put.Content.Headers.TryAddWithoutValidation("Content-Disposition", $"attachment; filename=\"{name}\"");
        put.Content.Headers.ContentMD5 = md5;
        await Stored(http, put, name);
    }
    else
    {
        upload = (await V3(http, Post($"{url}/v3/uploads/multipart", new JsonObject { ["filename"] = name, ["size_bytes"] = size.ToString(System.Globalization.CultureInfo.InvariantCulture) }, auth)))!;
        long partSize = long.Parse((string)upload["part_size_bytes"]!, System.Globalization.CultureInfo.InvariantCulture);
        var xml = new StringBuilder("<CompleteMultipartUpload>");
        using (var file = File.OpenRead(path))
        {
            int number = 0;
            foreach (var partUrl in upload["part_presigned_urls"]!.AsArray())
            {
                number++;
                byte[] data = new byte[(int)Math.Min(partSize, size - ((number - 1) * partSize))];
                file.Seek((number - 1) * partSize, SeekOrigin.Begin);
                file.ReadExactly(data);
                using var put = new HttpRequestMessage(HttpMethod.Put, (string)partUrl!) { Content = new ByteArrayContent(data) };
                put.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                string etag = await Stored(http, put, $"{name} part {number}");
                xml.Append(System.Globalization.CultureInfo.InvariantCulture, $"<Part><PartNumber>{number}</PartNumber><ETag>{etag.Replace("\"", "", StringComparison.Ordinal)}</ETag></Part>");
            }
        }

        using var complete = new HttpRequestMessage(HttpMethod.Post, (string)upload["complete_presigned_url"]!)
        {
            Content = new StringContent(xml.Append("</CompleteMultipartUpload>").ToString(), Encoding.UTF8, "application/xml"),
        };
        await Stored(http, complete, $"{name} (completing the parts)");
    }

    string id = (string)upload["id"]!;
    await V3(http, Post($"{url}/v3/uploads/{id}/finalise", new JsonObject(), auth));
    var waited = TimeSpan.Zero;
    for (var delay = TimeSpan.FromSeconds(2); ; delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 30)))
    {
        string? state = (string?)(await V3(http, Get($"{url}/v3/uploads/{id}", auth)))?["state"];
        if (state == "available")
        {
            return id;
        }

        if (waited > TimeSpan.FromMinutes(10))
        {
            throw new InvalidOperationException($"Nexus Mods was still processing {name} after 10 minutes (state {state}).");
        }

        await Task.Delay(delay);
        waited += delay;
    }
}

// Sends a request to Nexus' storage; returns the stored part's ETag.
static async Task<string> Stored(HttpClient http, HttpRequestMessage request, string what)
{
    using var answer = await http.SendAsync(request);
    if (!answer.IsSuccessStatusCode)
    {
        throw new InvalidOperationException($"Nexus Mods: storing {what} failed ({(int)answer.StatusCode}): {Shorten(await answer.Content.ReadAsStringAsync())}");
    }

    return answer.Headers.TryGetValues("ETag", out var tags) ? tags.First() : "";
}

// A v3 call: the answer's payload, out of its {"data": ...}.
static async Task<JsonNode?> V3(HttpClient http, HttpRequestMessage request)
{
    var answer = await Call(http, "Nexus Mods", request);
    return answer is JsonObject o && o.ContainsKey("data") ? o["data"] : answer;
}

// The plugin's zip for Nexus: its files under BepInEx/plugins/<name>/ (no manifest or icon: Nexus does not use them).
static string NexusPlugin(string zip, string name, string output)
{
    using var source = ZipFile.OpenRead(zip);
    using var target = ZipFile.Open(output, ZipArchiveMode.Create);
    foreach (var entry in source.Entries.Where(e => !e.FullName.EndsWith('/') && e.FullName is not ("manifest.json" or "icon.png")))
    {
        string path = entry.FullName.StartsWith("plugins/", StringComparison.Ordinal) ? entry.FullName["plugins/".Length..] : entry.FullName;
        var copy = target.CreateEntry($"BepInEx/plugins/{name}/{path}", CompressionLevel.Optimal);
        copy.LastWriteTime = entry.LastWriteTime;
        using var from = entry.Open();
        using var to = copy.Open();
        from.CopyTo(to);
    }

    return output;
}

// A .tar.gz repacked as a zip (Nexus takes no .tar.gz), keeping the Unix permissions and symbolic links.
static string TarToZip(string tarGz, string output)
{
    using var gzip = new GZipStream(File.OpenRead(tarGz), CompressionMode.Decompress);
    using var tar = new TarReader(gzip);
    using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
    while (tar.GetNextEntry() is { } entry)
    {
        string name = entry.Name.StartsWith("./", StringComparison.Ordinal) ? entry.Name[2..] : entry.Name;
        if (name.Length == 0)
        {
            continue;
        }

        uint mode = (uint)entry.Mode;
        // The kind of file and its permissions, as unzip reads them on Unix; the time, before the entry is written.
        (string path, uint kind) = entry.EntryType switch
        {
            TarEntryType.Directory => (name.TrimEnd('/') + "/", 0x4000u),
            TarEntryType.RegularFile or TarEntryType.V7RegularFile => (name, 0x8000u),
            TarEntryType.SymbolicLink => (name, 0xA000u),
            _ => throw new InvalidDataException($"{tarGz}: {name} is a {entry.EntryType}, which a zip cannot hold."),
        };
        var copy = zip.CreateEntry(path, CompressionLevel.Optimal);
        copy.ExternalAttributes = unchecked((int)((kind | (kind == 0xA000u ? 0x1FFu : mode)) << 16));
        copy.LastWriteTime = entry.ModificationTime;
        if (kind == 0xA000u)
        {
            // A symbolic link's target is the entry's content.
            using var to = copy.Open();
            to.Write(Encoding.UTF8.GetBytes(entry.LinkName));
        }
        else if (kind == 0x8000u && entry.DataStream is not null)
        {
            using var to = copy.Open();
            entry.DataStream.CopyTo(to);
        }
    }

    return output;
}

// Markdown as plain text, for Nexus' changelog (links keep their text, no ** or `).
static string PlainText(string markdown)
{
    string text = Regex.Replace(markdown, @"\[([^\]]+)\]\([^)]+\)", "$1");
    text = Regex.Replace(text, "^#+ ", "", RegexOptions.Multiline);
    return text.Replace("**", "", StringComparison.Ordinal).Replace("`", "", StringComparison.Ordinal).Trim();
}

static string Template(JsonNode file, string key, Release release) => Text(file, key).Replace("{version}", release.Version, StringComparison.Ordinal);

// ---- Requests.

static HttpRequestMessage Get(string url, params (string Name, string Value)[] headers) => Request(HttpMethod.Get, url, null, headers);

static HttpRequestMessage Post(string url, JsonNode body, params (string Name, string Value)[] headers) => Request(HttpMethod.Post, url, body, headers);

static HttpRequestMessage Request(HttpMethod method, string url, JsonNode? body, (string Name, string Value)[] headers)
{
    var request = new HttpRequestMessage(method, url);
    if (body is not null)
    {
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
    }

    foreach (var (name, value) in headers)
    {
        request.Headers.TryAddWithoutValidation(name, value);
    }

    return request;
}

static (string, string) Bearer(string token) => ("Authorization", $"Bearer {token}");

// Sends a request and returns the JSON it answers (null for none). An error answer throws SiteException with
// what the site said; missingIsNull: a 404 answers null instead.
static async Task<JsonNode?> Call(HttpClient http, string site, HttpRequestMessage request, bool missingIsNull = false)
{
    using (request)
    {
        using var answer = await http.SendAsync(request);
        string text = await answer.Content.ReadAsStringAsync();
        if (missingIsNull && answer.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!answer.IsSuccessStatusCode)
        {
            // The URL without its query: a presigned URL's query is a credential.
            string where = request.RequestUri!.GetLeftPart(UriPartial.Path);
            throw new SiteException($"{site} answered {(int)answer.StatusCode} to {request.Method} {where}: {Shorten(text)}", (int)answer.StatusCode);
        }

        return text.Length == 0 ? null : JsonNode.Parse(text);
    }
}

static string Shorten(string text) => text.Length > 500 ? text[..500] + "…" : text;

static string Token(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } token
    ? token
    : throw new InvalidOperationException($"{name} is not set (CircleCI: the release context).");

static string Text(JsonNode node, string key) => (string?)node[key] ?? throw new InvalidDataException($"tools/release/sites.json: no {key}.");

static string[] Texts(JsonNode node, string key) => [.. node[key]!.AsArray().Select(v => (string)v!)];

// One release's packages: the plugin's zip (with its manifest and icon, for the sites' checks) and the editor's.
internal sealed record Release(string Version, string Name, string Bridge, byte[] Manifest, byte[] Icon, string Linux, string Windows);

// A site's error answer, with its HTTP status.
internal sealed class SiteException(string message, int status) : InvalidOperationException(message)
{
    public int Status { get; } = status;
}
