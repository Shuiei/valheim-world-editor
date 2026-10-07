using System.Globalization;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

// Who builds in the editor: the player id written as "creator" on new pieces (WorldSave.Builder), so
// the game treats them as player built (full materials back, a base for fires, a target for raids,
// owned wards and private chests). Shared by both editors.
public static class Builders
{
	public static readonly int CreatorKey = StableHash.Of("creator");

	// When no player is known: still a builder, so pieces are player built (wards and private chests
	// answer to nobody until another one is chosen).
	public const long Unknown = 1;

	// The builder to start with: the world's main builder, else this computer's first character, else
	// a player named in the world, else Unknown.
	public static long Default(WorldSave w) =>
		w.TopBuilder != 0 ? w.TopBuilder : Characters.Local().FirstOrDefault()?.Id ?? (w.PlayerNames.Count > 0 ? w.PlayerNames.Keys.First() : Unknown);

	public sealed record Player(long Id, string? Name, int Pieces, bool Local)
	{
		public string Label => $"{Name ?? $"Player {Id}"}{(Local ? " (this computer)" : "")}{(Pieces > 0 ? $" · {Pieces} piece{(Pieces == 1 ? "" : "s")}" : "")}";
	}

	// The choices: the world's builders (most pieces first), this computer's characters, named players.
	public static List<Player> Players(WorldSave w)
	{
		var local = Characters.Local();
		var ids = w.Creators.OrderByDescending(c => c.Value).Select(c => c.Key)
			.Concat(local.Select(c => c.Id)).Concat(w.PlayerNames.Keys).Append(WorldSave.Builder).Where(id => id != 0).Distinct();
		return ids.Select(id => new Player(id, local.FirstOrDefault(c => c.Id == id)?.Name ?? w.PlayerNames.GetValueOrDefault(id) ?? (id == Unknown ? "Unknown player" : null),
			w.Creators.GetValueOrDefault(id), local.Any(c => c.Id == id))).ToList();
	}

	// A piece made player built: a copy of its data with the builder, or null when it is not a kind
	// players build or it already has a builder.
	public static ZdoData? Claimed(byte[] bytes, long builder)
	{
		ZdoData z = ZdoData.Parse(bytes);
		if (PieceCatalog.Get(z.Prefab)?.Tool == null || z.LongList.FirstOrDefault(l => l.Key == CreatorKey).Value != 0)
		{
			return null;
		}
		z.Set("longs", CreatorKey, builder.ToString(CultureInfo.InvariantCulture));
		return z;
	}
}
