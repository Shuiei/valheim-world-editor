using System.Globalization;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

// Who builds in the editor: the player id written as "creator" on new pieces, so the game treats
// them as player built (full materials back, a base for fires, a target for raids, owned wards and
// private chests). The choices: the world's builders and named players, and this computer's characters.
public static class BuilderEndpoints
{
	private static readonly int CreatorKey = StableHash.Of("creator");

	public static void Map(WebApplication app, Func<WorldSave> world, EditStore edits, Func<object> pending)
	{
		app.MapGet("/api/builders", () =>
		{
			WorldSave w = world();
			List<Characters.Character> local = Characters.Local();
			IEnumerable<long> ids = w.Creators.OrderByDescending(c => c.Value).Select(c => c.Key)
				.Concat(local.Select(c => c.Id)).Concat(w.PlayerNames.Keys).Append(WorldSave.Builder).Where(id => id != 0).Distinct();
			return new
			{
				builder = WorldSave.Builder.ToString(CultureInfo.InvariantCulture),
				players = ids.Select(id => new
				{
					// As text: JavaScript numbers cannot hold every 64-bit id.
					id = id.ToString(CultureInfo.InvariantCulture),
					name = local.FirstOrDefault(c => c.Id == id)?.Name ?? w.PlayerNames.GetValueOrDefault(id) ?? (id == Unknown ? "Unknown player" : null),
					pieces = w.Creators.GetValueOrDefault(id),
					local = local.Any(c => c.Id == id),
				}),
			};
		});

		app.MapPost("/api/builder", (BuilderChoice req) =>
		{
			// 0: nobody, new pieces get no builder (the game then takes them for parts of a ruin).
			if (!long.TryParse(req.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id))
			{
				return Results.BadRequest("A player id (a whole number) is needed.");
			}
			WorldSave.Builder = id;
			return Results.Ok(new { builder = req.Id });
		});

		// Make pieces player built: each listed piece of a kind players build that has no builder is
		// replaced by a copy with the chosen builder (one undo step on the page, like an edit).
		app.MapPost("/api/objects/claim", (ClaimRequest req) =>
		{
			WorldSave w = world();
			if (WorldSave.Builder == 0)
			{
				return Results.BadRequest("Built by is set to Nobody: choose a player first (View panel, Building).");
			}
			List<object> changed = new();
			foreach (IdPair p in req.Objects ?? new())
			{
				byte[]? bytes = ObjectEndpoints.Bytes(w, edits, p.Id);
				if (bytes == null || p.NewId >= 0)
				{
					continue;
				}
				ZdoData z = ZdoData.Parse(bytes);
				if (PieceCatalog.Get(z.Prefab)?.Tool == null || z.LongList.FirstOrDefault(l => l.Key == CreatorKey).Value != 0)
				{
					continue;
				}
				z.Set("longs", CreatorKey, WorldSave.Builder.ToString(CultureInfo.InvariantCulture));
				edits.AddObjects(new[] { new NewObject(p.NewId, z.Prefab, z.Position, z.Rotation, 0f, null, false, z.Serialize()) });
				edits.SetDeleted(new[] { p.Id }, true);
				changed.Add(new { id = p.Id, detail = ObjectEndpoints.Describe(p.NewId, z) });
			}
			return Results.Json(new { pending = pending(), changed });
		});
	}

	// When no player is known: still a builder, so pieces are player built (wards and private chests
	// answer to nobody until another one is chosen).
	public const long Unknown = 1;

	// The builder to start with: the world's main builder, else this computer's first character, else
	// a player named in the world, else Unknown.
	public static long Default(WorldSave w) =>
		w.TopBuilder != 0 ? w.TopBuilder : Characters.Local().FirstOrDefault()?.Id ?? (w.PlayerNames.Count > 0 ? w.PlayerNames.Keys.First() : Unknown);
}

public sealed record BuilderChoice(string Id);

public sealed record IdPair(int Id, int NewId);

public sealed record ClaimRequest(List<IdPair>? Objects);
