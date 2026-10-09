using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

// Homestead (sighsorry-Homestead), the in-game building mod: its blueprints are the editor's building
// library. They are PlanBuild .blueprint files with Homestead's own header lines, a .png picture
// beside each, in Homestead/Blueprints of Valheim's save folder (next to worlds_local), where Homestead
// lists them in its hammer tab and players build them in game with a materials chest. Each piece is
// "prefab;category;x;y;z;qx;qy;qz;qw;"text";sx;sy;sz", measured from the blueprint's anchor: the
// middle of the saved area at ground level, turned with it (ZoneBlueprintFileFormat.cs, Homestead's
// source on GitHub).
public static class Homestead
{
	public const string PageUrl = "https://thunderstore.io/c/valheim/p/sighsorry/Homestead/";
	public const int FormatVersion = 1;

	// Whether Homestead is installed (in the game's BepInEx or a mod manager profile, its version and
	// where), and its blueprint folder (made when the first blueprint is saved).
	public sealed record Status(bool Installed, string? Version, List<string> Where, string Folder);

	public static Status Find(AppSettings settings)
	{
		var where = new List<string>();
		string? version = null;
		foreach (var (dir, place) in LocalGame.BepInExFolders(settings))
		{
			string plugins = Path.Combine(dir, "plugins");
			if (!Directory.Exists(plugins))
			{
				continue;
			}
			try
			{
				foreach (string dll in Directory.EnumerateFiles(plugins, "Homestead.dll", SearchOption.AllDirectories))
				{
					where.Add(place);
					version ??= VersionOf(dll);
					break;
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
			}
		}
		return new Status(where.Count > 0, version, where, Folder());
	}

	// The mod package's manifest.json (as mod managers install it), else the DLL's own version.
	private static string? VersionOf(string dll)
	{
		try
		{
			string manifest = Path.Combine(Path.GetDirectoryName(dll)!, "manifest.json");
			if (File.Exists(manifest) && JsonNode.Parse(File.ReadAllText(manifest))?["version_number"] is JsonNode v)
			{
				return (string?)v;
			}
			return FileVersionInfo.GetVersionInfo(dll).FileVersion;
		}
		catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
		{
			return null;
		}
	}

	// Another folder than the player's (the tests': they must never write into the game's folders).
	public static string? FolderOverride { get; set; }

	// Homestead/Blueprints in Valheim's save folder: the one that has it already, else the first save
	// folder the game made (where its worlds are), else the usual one for this system.
	public static string Folder()
	{
		if (FolderOverride != null)
		{
			return FolderOverride;
		}
		var saves = Places.WorldRoots().Select(r => Path.GetDirectoryName(r)!).Distinct().ToList();
		string Of(string save) => Path.Combine(save, "Homestead", "Blueprints");
		return saves.Select(Of).FirstOrDefault(Directory.Exists) ?? Of(saves.FirstOrDefault(Directory.Exists) ?? saves[0]);
	}

	// What a player writes about a blueprint: its name, a description and tags (house, viking, stone…).
	// Homestead reads the name; the description is PlanBuild's "#Description:" line and the tags the
	// editor's own "#Tags:" line, which Homestead skips (any header line before "#Pieces").
	public sealed record Details(string Name, string Description, List<string> Tags)
	{
		// "house, Viking,stone ,house" → house, Viking, stone.
		public static List<string> ParseTags(string? text) => (text ?? "").Split(',', ';').Select(t => t.Trim()).Where(t => t.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
	}

	// One blueprint of the folder: its name, creator, description and tags, how many of each piece kind
	// (for its cost) and its picture (.png beside it).
	public sealed record Entry(string Path, string Name, string? Creator, string? World, int Pieces, string? Picture, DateTime Saved)
	{
		public string Description { get; init; } = "";
		public List<string> Tags { get; init; } = new();
		public Dictionary<string, int> Kinds { get; init; } = new();
		public Details Details => new(Name, Description, Tags);
	}

	public static List<Entry> List(string folder)
	{
		if (!Directory.Exists(folder))
		{
			return new();
		}
		return Directory.GetFiles(folder, "*.blueprint").Select(Read).OfType<Entry>().OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
	}

	// One blueprint file (null when it cannot be read).
	public static Entry? Read(string file)
	{
		try
		{
			string? name = null, creator = null, world = null, description = null, tags = null;
			int pieces = 0;
			var kinds = new Dictionary<string, int>();
			bool inPieces = false;
			foreach (string raw in File.ReadLines(file))
			{
				string line = raw.Trim();
				if (line.StartsWith("#Name:", StringComparison.OrdinalIgnoreCase)) name = line[6..].Trim();
				else if (line.StartsWith("#Creator:", StringComparison.OrdinalIgnoreCase)) creator = line[9..].Trim();
				else if (line.StartsWith("#Description:", StringComparison.OrdinalIgnoreCase)) description = line[13..].Trim().Trim('"');
				else if (line.StartsWith("#Tags:", StringComparison.OrdinalIgnoreCase)) tags = line[6..];
				else if (line.StartsWith("#HomesteadWorld:", StringComparison.OrdinalIgnoreCase)) world = line[16..].Trim();
				else if (line.StartsWith('#')) inPieces = line.Equals("#Pieces", StringComparison.OrdinalIgnoreCase);
				else if (inPieces && line.Length > 0)
				{
					pieces++;
					string kind = line.Split(';')[0].Trim();
					kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
				}
			}
			string png = System.IO.Path.ChangeExtension(file, ".png");
			return new Entry(file, string.IsNullOrWhiteSpace(name) ? System.IO.Path.GetFileNameWithoutExtension(file) : name, creator,
				string.IsNullOrWhiteSpace(world) ? null : world, pieces, File.Exists(png) ? png : null, File.GetLastWriteTime(file))
			{
				Description = description ?? "", Tags = Details.ParseTags(tags), Kinds = kinds,
			};
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	// A blueprint's name, description and tags changed in its file (the pieces stay as they are).
	public static string WithDetails(string text, Details details)
	{
		var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
		int pieces = lines.FindIndex(l => l.Trim().Equals("#Pieces", StringComparison.OrdinalIgnoreCase));
		if (pieces < 0)
		{
			pieces = lines.Count;
		}
		var head = lines.Take(pieces).Where(l => !(l.StartsWith("#Name:", StringComparison.OrdinalIgnoreCase) || l.StartsWith("#Description:", StringComparison.OrdinalIgnoreCase)
			|| l.StartsWith("#Tags:", StringComparison.OrdinalIgnoreCase))).ToList();
		var mine = new List<string> { "#Name:" + Header(details.Name), "#Description:" + Header(details.Description) };
		if (details.Tags.Count > 0)
		{
			mine.Add("#Tags:" + Header(string.Join(", ", details.Tags)));
		}
		int creator = head.FindIndex(l => l.StartsWith("#Creator:", StringComparison.OrdinalIgnoreCase));
		head.InsertRange(0, mine.Take(1));
		head.InsertRange(creator < 0 ? 1 : creator + 2, mine.Skip(1));
		return string.Join("\n", head.Concat(lines.Skip(pieces)));
	}

	// A file name for a blueprint name: what Windows and Linux both allow.
	public static string FileName(string name)
	{
		string f = new(name.Trim().Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) || c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' ? '_' : c).ToArray());
		f = f.Trim('.', ' ');
		return (f.Length == 0 ? "blueprint" : f.Length > 64 ? f[..64] : f) + ".blueprint";
	}

	// The objects of a copy (the editor's clipboard format) as a Homestead blueprint. dx, dz are metres
	// from the copy's middle, dy from its ground: Homestead's anchor. Rotations become quaternions.
	public static string Write(JsonObject clip, string name, string creator, string? world, DateTime saved, string description = "", IReadOnlyCollection<string>? tags = null)
	{
		var objs = (clip["objects"] as JsonArray ?? new()).OfType<JsonObject>().Select(o => (
			Name: (string?)o["name"] ?? "", X: F(o["dx"]), Y: F(o["dy"]), Z: F(o["dz"]),
			Rot: BlueprintFormats.FromEuler(new Vector3(F(o["rx"]), F(o["ry"]), F(o["rz"]))), Scale: F(o["scale"]))).Where(o => o.Name.Length > 0).ToList();
		float radius = objs.Count == 0 ? 0 : MathF.Sqrt(objs.Max(o => o.X * o.X + o.Z * o.Z)) + 1f;
		StringBuilder sb = new();
		sb.Append("#Name:").Append(Header(name)).Append('\n');
		sb.Append("#Creator:").Append(Header(creator)).Append('\n');
		sb.Append("#Description:").Append(Header(description)).Append('\n');
		if (tags is { Count: > 0 })
		{
			sb.Append("#Tags:").Append(Header(string.Join(", ", tags))).Append('\n');
		}
		sb.Append("#Category:Blueprints\n");
		sb.Append("#HomesteadVersion:").Append(FormatVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
		sb.Append("#HomesteadWorld:").Append(Header(world ?? "")).Append('\n');
		sb.Append("#HomesteadSavedAt:").Append(new DateTimeOffset(saved).ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)).Append('\n');
		sb.Append("#HomesteadRadius:").Append(S(radius)).Append('\n');
		sb.Append("#Pieces\n");
		foreach (var o in objs.OrderBy(o => o.Y).ThenBy(o => o.X).ThenBy(o => o.Z))
		{
			float s = o.Scale > 0 ? o.Scale : 1f;
			sb.Append(string.Join(";", Header(o.Name).Replace(";", "", StringComparison.Ordinal), "Building", S(o.X), S(o.Y), S(o.Z),
				S(o.Rot.X), S(o.Rot.Y), S(o.Rot.Z), S(o.Rot.W), "\"\"", S(s), S(s), S(s))).Append('\n');
		}
		return sb.ToString();
	}

	private static float F(JsonNode? n) => n == null ? 0 : float.Parse(n.ToJsonString(), CultureInfo.InvariantCulture);

	// Homestead writes numbers with three decimals at most.
	private static string S(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

	private static string Header(string v) => v.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
