using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

// Blueprint files of other Valheim mods, read into the editor's clipboard format and written from it:
//   PlanBuild .blueprint   "#Name:", "#Creator:", "#Description:", "#Category:", optional "#SnapPoints" and
//                          "#Terrain" sections, then "#Pieces": name;category;x;y;z;qx;qy;qz;qw;info;sx;sy;sz
//   .vbuild (BuildShare)   one piece per line: name qx qy qz qw x y z
// Positions are relative to the blueprint's origin along the world axes; rotations are Unity
// quaternions. Terrain entries (shape;x;y;z;radius;rotation;smooth;paint) level and paint the ground.
public static class BlueprintFormats
{
	// Data: values of the object's data the editor keeps for it (its own #EditorData lines).
	public sealed record Piece(string Name, Vector3 Position, Vector3 Euler, float Scale, IReadOnlyList<TerrainEditor.Editing.ObjectField>? Data = null);

	public sealed record TerrainMod(string Shape, Vector3 Position, float Radius, int Rotation, string Paint);

	// Ruin: written by the editor as a ruin (no builder; #EditorRuin), as generated dungeons are.
	public sealed record Parsed(string Name, string? Creator, string? Description, List<Piece> Pieces, List<TerrainMod> Terrain, int SkippedLines, bool Ruin = false);

	// The editor's own header lines (other mods skip header lines they do not know): an object's data,
	// "#EditorData:n;section;key;value" (n: the object's line among the pieces, from 0), and a building
	// written as a ruin. Homestead and PlanBuild build without them.
	public const string DataHeader = "#EditorData:", RuinHeader = "#EditorRuin:1";

	public static Parsed Parse(string fileName, string text)
	{
		string ext = Path.GetExtension(fileName).ToLowerInvariant();
		bool vbuild = ext == ".vbuild";
		string name = Path.GetFileNameWithoutExtension(fileName);
		string? creator = null, description = null;
		List<Piece> pieces = new();
		List<TerrainMod> terrain = new();
		int skipped = 0, line0 = 0;
		bool ruin = false;
		var data = new Dictionary<int, List<TerrainEditor.Editing.ObjectField>>();
		var lines = new List<int>();
		string section = "pieces";
		foreach (string raw in text.Split('\n'))
		{
			string line = raw.Trim().TrimStart('﻿');
			if (line.Length == 0)
			{
				continue;
			}
			if (line.StartsWith("#Name:", StringComparison.Ordinal)) { name = line[6..].Trim() is { Length: > 0 } n ? n : name; continue; }
			if (line.StartsWith("#Creator:", StringComparison.Ordinal)) { creator = line[9..].Trim(); continue; }
			if (line.StartsWith("#Description:", StringComparison.Ordinal)) { description = Unquote(line[13..].Trim()); continue; }
			if (line.StartsWith("#Category:", StringComparison.Ordinal)) { continue; }
			if (line == RuinHeader) { ruin = true; continue; }
			if (line.StartsWith(DataHeader, StringComparison.Ordinal))
			{
				string rest = line[DataHeader.Length..];
				int bar = rest.IndexOf(';');
				if (bar > 0 && int.TryParse(rest[..bar], NumberStyles.Integer, CultureInfo.InvariantCulture, out int at) && TerrainEditor.Editing.ObjectField.Parse(rest[(bar + 1)..]) is { } f)
				{
					(data.TryGetValue(at, out var l) ? l : data[at] = new()).Add(f);
				}
				continue;
			}
			if (line == "#SnapPoints") { section = "snap"; continue; }
			if (line == "#Terrain") { section = "terrain"; continue; }
			if (line == "#Pieces") { section = "pieces"; continue; }
			if (line.StartsWith('#'))
			{
				// Another mod's section (InfinityHammer's #TerrainHeight...): skipped like PlanBuild does.
				section = "skip";
				continue;
			}
			try
			{
				switch (section)
				{
					case "terrain":
						terrain.Add(ParseTerrain(line));
						break;
					case "pieces":
						int n = line0++;
						pieces.Add(vbuild ? ParseVBuild(line) : ParseBlueprintPiece(line));
						lines.Add(n);
						break;
				}
			}
			catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or OverflowException)
			{
				skipped++;
			}
		}
		if (data.Count > 0 || ruin)
		{
			for (int k = 0; k < pieces.Count; k++)
			{
				var d = data.TryGetValue(lines[k], out var held) ? held : new List<TerrainEditor.Editing.ObjectField>();
				if (ruin)
				{
					d.Add(TerrainEditor.Editing.ObjectField.NoBuilder);
				}
				pieces[k] = pieces[k] with { Data = d };
			}
		}
		return new Parsed(name, creator, description, pieces, terrain, skipped, ruin);
	}

	private static Piece ParseBlueprintPiece(string line)
	{
		// Old PlanBuild files were written with a comma as decimal separator.
		if (line.Contains(',') && !line.Contains('"'))
		{
			line = line.Replace(',', '.');
		}
		string[] p = line.Split(';');
		Quaternion q = Quaternion.Normalize(new Quaternion(F(p[5]), F(p[6]), F(p[7]), F(p[8])));
		float scale = p.Length > 12 ? F(p[10]) : 1f;
		return new Piece(PrefabName(p[0]), new Vector3(F(p[2]), F(p[3]), F(p[4])), ToEuler(q), scale);
	}

	private static Piece ParseVBuild(string line)
	{
		if (line.Contains(','))
		{
			line = line.Replace(',', '.');
		}
		string[] p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		Quaternion q = Quaternion.Normalize(new Quaternion(F(p[1]), F(p[2]), F(p[3]), F(p[4])));
		return new Piece(PrefabName(p[0]), new Vector3(F(p[5]), F(p[6]), F(p[7])), ToEuler(q), 1f);
	}

	private static TerrainMod ParseTerrain(string line)
	{
		string[] p = line.Split(';');
		return new TerrainMod(p[0].ToLowerInvariant(), new Vector3(F(p[1]), F(p[2]), F(p[3])), F(p[4]),
			(int)MathF.Round(F(p[5])), p.Length > 7 ? p[7] : "");
	}

	// "piece_chest(Clone)" -> "piece_chest", as PlanBuild does.
	private static string PrefabName(string s) => s.Split('(')[0].Trim();

	private static string Unquote(string s) => s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1].Replace("\\\"", "\"").Replace("\\n", "\n") : s;

	private static float F(string s) => string.IsNullOrWhiteSpace(s) ? 0f : float.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);

	private static string S(float f) => f.ToString("0.#####", CultureInfo.InvariantCulture);

	// Unity's Euler angles (degrees, applied z, then x, then y: R = Ry * Rx * Rz) of a quaternion.
	public static Vector3 ToEuler(Quaternion q)
	{
		q = Quaternion.Normalize(q);
		float m02 = 2f * (q.X * q.Z + q.W * q.Y), m22 = 1f - 2f * (q.X * q.X + q.Y * q.Y);
		float m12 = 2f * (q.Y * q.Z - q.W * q.X), m10 = 2f * (q.X * q.Y + q.W * q.Z), m11 = 1f - 2f * (q.X * q.X + q.Z * q.Z);
		float m00 = 1f - 2f * (q.Y * q.Y + q.Z * q.Z), m20 = 2f * (q.X * q.Z - q.W * q.Y);
		float x, y, z;
		if (MathF.Abs(m12) < 0.99999f)
		{
			x = MathF.Asin(-m12);
			y = MathF.Atan2(m02, m22);
			z = MathF.Atan2(m10, m11);
		}
		else
		{
			// Looking straight up or down: only the sum of y and z is defined.
			x = m12 < 0 ? MathF.PI / 2 : -MathF.PI / 2;
			y = MathF.Atan2(-m20, m00);
			z = 0f;
		}
		return new Vector3(Deg(x), Deg(y), Deg(z));
		static float Deg(float r) { float d = r * 180f / MathF.PI; d %= 360f; return d < 0 ? d + 360f : d; }
	}

	// Unity's Quaternion.Euler: z, then x, then y.
	public static Quaternion FromEuler(Vector3 e)
	{
		const float k = MathF.PI / 360f;
		Quaternion qx = new(MathF.Sin(e.X * k), 0, 0, MathF.Cos(e.X * k)), qy = new(0, MathF.Sin(e.Y * k), 0, MathF.Cos(e.Y * k)), qz = new(0, 0, MathF.Sin(e.Z * k), MathF.Cos(e.Z * k));
		// System.Numerics multiplies right to left like Unity: qy * qx * qz applies qz first.
		return Mul(Mul(qy, qx), qz);
	}

	private static Quaternion Mul(Quaternion a, Quaternion b) => new(
		a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
		a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
		a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
		a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

	// The editor's clipboard (as stored: heights in cm, -32768 = no ground; weights and paint in 1/255)
	// from a parsed file. The lowest piece rests on the point where it is pasted; ground from terrain
	// entries is levelled to their height, relative to the same point.
	public static JsonObject ToClip(Parsed bp, Func<string, bool> known, out List<string> unknown)
	{
		List<Piece> pieces = bp.Pieces.Where(p => known(p.Name)).ToList();
		unknown = bp.Pieces.Select(p => p.Name).Where(n => !known(n)).Distinct().OrderBy(n => n).ToList();
		float minY = pieces.Count > 0 ? pieces.Min(p => p.Position.Y) : bp.Terrain.Count > 0 ? bp.Terrain.Min(t => t.Position.Y) : 0f;
		float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
		void Grow(float x, float z) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); z0 = Math.Min(z0, z); z1 = Math.Max(z1, z); }
		foreach (Piece p in pieces) Grow(p.Position.X, p.Position.Z);
		foreach (TerrainMod t in bp.Terrain) { Grow(t.Position.X - t.Radius, t.Position.Z - t.Radius); Grow(t.Position.X + t.Radius, t.Position.Z + t.Radius); }
		if (x0 > x1)
		{
			x0 = x1 = z0 = z1 = 0f;
		}
		float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f;
		JsonArray objects = new();
		foreach (Piece p in pieces)
		{
			objects.Add(new JsonObject
			{
				["name"] = p.Name, ["dx"] = R(p.Position.X - cx), ["dz"] = R(p.Position.Z - cz), ["dy"] = R(p.Position.Y - minY),
				["rx"] = R(p.Euler.X), ["ry"] = R(p.Euler.Y), ["rz"] = R(p.Euler.Z), ["scale"] = MathF.Abs(p.Scale - 1f) < 1e-3f ? 0 : R(p.Scale), ["sourceId"] = null,
			});
			if (p.Data is { Count: > 0 } fields)
			{
				((JsonObject)objects[^1]!)["data"] = DataJson(fields);
			}
		}
		int w = 1, h = 1;
		int[] rel = { -32768 }, wt = { 0 }, pnt = { -255, -255, -255, -255 };
		if (bp.Terrain.Count > 0)
		{
			w = (int)MathF.Ceiling(x1 - x0) + 1;
			h = (int)MathF.Ceiling(z1 - z0) + 1;
			rel = Enumerable.Repeat(-32768, w * h).ToArray();
			wt = new int[w * h];
			pnt = Enumerable.Repeat(-255, w * h * 4).ToArray();
			for (int iz = 0; iz < h; iz++)
			{
				for (int ix = 0; ix < w; ix++)
				{
					float x = cx + ix - (w - 1) / 2f, z = cz + iz - (h - 1) / 2f;
					// Later entries are applied over earlier ones, as in game.
					TerrainMod? hit = bp.Terrain.LastOrDefault(t => Covers(t, x, z));
					if (hit == null)
					{
						continue;
					}
					int i = iz * w + ix;
					rel[i] = (int)MathF.Round((hit.Position.Y - minY) * 100f);
					wt[i] = 255;
					float[]? paint = PaintOf(hit.Paint);
					if (paint != null)
					{
						for (int c = 0; c < 4; c++)
						{
							pnt[i * 4 + c] = (int)MathF.Round(paint[c] * 255f);
						}
					}
				}
			}
		}
		const float pad = 1f;
		JsonArray poly = new(
			Point(x0 - cx - pad, z0 - cz - pad), Point(x1 - cx + pad, z0 - cz - pad),
			Point(x1 - cx + pad, z1 - cz + pad), Point(x0 - cx - pad, z1 - cz + pad));
		return new JsonObject
		{
			["w"] = w, ["h"] = h,
			["rel"] = new JsonArray(rel.Select(v => (JsonNode)v).ToArray()),
			["wt"] = new JsonArray(wt.Select(v => (JsonNode)v).ToArray()),
			["pnt"] = new JsonArray(pnt.Select(v => (JsonNode)v).ToArray()),
			["objects"] = objects,
			["poly"] = poly,
			["name"] = bp.Name,
		};
		static JsonObject Point(float gx, float gz) => new() { ["gx"] = R(gx), ["gz"] = R(gz) };
	}

	private static float R(float v) => MathF.Round(v * 1000f) / 1000f;

	// An object's data in the clipboard format: its fields as text (ObjectField).
	public static JsonArray DataJson(IEnumerable<TerrainEditor.Editing.ObjectField> fields) => new(fields.Select(f => (JsonNode)f.ToString()).ToArray());

	public static List<TerrainEditor.Editing.ObjectField>? DataOf(JsonNode? data) =>
		data is JsonArray a && a.Count > 0 ? a.Select(n => TerrainEditor.Editing.ObjectField.Parse((string?)n ?? "")).OfType<TerrainEditor.Editing.ObjectField>().ToList() : null;

	private static bool Covers(TerrainMod t, float x, float z)
	{
		float dx = x - t.Position.X, dz = z - t.Position.Z;
		if (t.Shape != "square")
		{
			return dx * dx + dz * dz <= t.Radius * t.Radius;
		}
		// A square turned by the entry's rotation (Unity yaw: clockwise seen from above).
		float a = -t.Rotation * MathF.PI / 180f, c = MathF.Cos(a), s = MathF.Sin(a);
		float lx = dx * c + dz * s, lz = -dx * s + dz * c;
		return MathF.Abs(lx) <= t.Radius && MathF.Abs(lz) <= t.Radius;
	}

	// PlanBuild's paint names -> the game's paint mask (r dirt, g cultivated, b paved, a vegetation).
	private static float[]? PaintOf(string paint) => paint.ToLowerInvariant() switch
	{
		var p when p.Contains("dirt") => new[] { 1f, 0f, 0f, 1f },
		var p when p.Contains("cultiv") => new[] { 0f, 1f, 0f, 1f },
		var p when p.Contains("paved") => new[] { 0f, 0f, 1f, 1f },
		_ => null,
	};

	// The objects of a clipboard (blueprint file content) as a PlanBuild .blueprint or a .vbuild file.
	// Ground is not written: PlanBuild's terrain entries cannot describe free-form ground.
	public static string Write(JsonObject clip, string format, string name, Func<string, int> category)
	{
		var objs = (clip["objects"] as JsonArray ?? new()).OfType<JsonObject>().Select(o => (
			Name: (string?)o["name"] ?? "", X: (float?)o["dx"] ?? 0, Y: (float?)o["dy"] ?? 0, Z: (float?)o["dz"] ?? 0,
			Rot: FromEuler(new Vector3((float?)o["rx"] ?? 0, (float?)o["ry"] ?? 0, (float?)o["rz"] ?? 0)), Scale: (float?)o["scale"] ?? 0)).ToList();
		// PlanBuild measures from the lowest corner; ground level stays where the copy had it.
		float minX = objs.Count > 0 ? objs.Min(o => o.X) : 0, minZ = objs.Count > 0 ? objs.Min(o => o.Z) : 0;
		StringBuilder sb = new();
		if (format == "vbuild")
		{
			foreach (var o in objs.OrderBy(o => o.Y))
			{
				sb.Append(o.Name).Append(' ').Append(S(o.Rot.X)).Append(' ').Append(S(o.Rot.Y)).Append(' ').Append(S(o.Rot.Z)).Append(' ').Append(S(o.Rot.W))
					.Append(' ').Append(S(o.X - minX)).Append(' ').Append(S(o.Y)).Append(' ').Append(S(o.Z - minZ)).Append('\n');
			}
			return sb.ToString();
		}
		string[] categories = { "Misc", "Crafting", "BuildingWorkbench", "BuildingStonecutter", "Furniture", "DeepNorth", "Feasts", "Food", "Meads" };
		sb.Append("#Name:").Append(name).Append('\n');
		sb.Append("#Creator:Valheim World Editor\n");
		sb.Append("#Description:\"\"\n");
		sb.Append("#Category:Misc\n");
		sb.Append("#Pieces\n");
		foreach (var o in objs.OrderBy(o => o.Y).ThenBy(o => o.X).ThenBy(o => o.Z))
		{
			int cat = category(o.Name);
			float s = o.Scale > 0 ? o.Scale : 1f;
			sb.Append(string.Join(";", o.Name, cat >= 0 && cat < categories.Length ? categories[cat] : "Misc",
				S(o.X - minX), S(o.Y), S(o.Z - minZ), S(o.Rot.X), S(o.Rot.Y), S(o.Rot.Z), S(o.Rot.W), "\"\"", S(s), S(s), S(s))).Append('\n');
		}
		return sb.ToString();
	}
}
