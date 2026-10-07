using System.Text.RegularExpressions;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

// The kinds objects are sorted into for the View switches, the same rules as the web editor's
// objectKind (wwwroot/editor/objects.js): by name, building pieces placed by players apart.
public enum ObjectKind
{
	Buildings,
	Ruins,
	Trees,
	Rocks,
	Ore,
	Bushes,
	Pickables,
	Other,
}

public static class ObjectKinds
{
	public static readonly ObjectKind[] All = Enum.GetValues<ObjectKind>();

	public static string Label(ObjectKind k) => k switch
	{
		ObjectKind.Buildings => "Your buildings",
		ObjectKind.Ruins => "Ruins & structures",
		ObjectKind.Trees => "Trees & logs",
		ObjectKind.Rocks => "Rocks",
		ObjectKind.Ore => "Ore & deposits",
		ObjectKind.Bushes => "Bushes & shrubs",
		ObjectKind.Pickables => "Pickables",
		_ => "Other objects",
	};

	// Shown when the editor opens, as in the web editor's View panel (spoilers stay hidden).
	public static bool ShownAtFirst(ObjectKind k) => k is ObjectKind.Buildings or ObjectKind.Trees or ObjectKind.Rocks;

	private static readonly Regex Ruin = new(@"^goblin_|^shipwreck_|^Statue|^BossStone_|^dungeon_|crypt_gate|^CastleKit_|^StartPlatform|^Beehive|^CargoCrate|^barrell|^RockDolmen|^TreasureChest", RegexOptions.IgnoreCase);
	private static readonly Regex Pickable = new(@"^Pickable_|^Pickable|mushroom|Dandelion|Thistle", RegexOptions.IgnoreCase);
	private static readonly Regex Tree = new(@"tree|beech|birch|oak|^fir|pine|stub|log|root|yggdrasil|ashwood|sapling", RegexOptions.IgnoreCase);
	private static readonly Regex Tomb = new(@"tombstone", RegexOptions.IgnoreCase);
	private static readonly Regex Ore = new(@"minerock|copper|tin|silver|vein|obsidian|mudpile|guck|leviathan|tar|flametal", RegexOptions.IgnoreCase);
	private static readonly Regex Rock = new(@"rock|stone|ice|boulder|cliff|pillar", RegexOptions.IgnoreCase);
	private static readonly Regex Bush = new(@"bush|shrub|fern|ormbunke|vines|cloudberry", RegexOptions.IgnoreCase);
	private static readonly HashSet<string> PieceNames = new(PieceCatalog.Names, StringComparer.Ordinal);

	// builtPiece: a building piece a player placed (it has a builder); the same piece without one is
	// part of a ruin.
	public static ObjectKind Of(string? name, bool builtPiece)
	{
		if (builtPiece)
		{
			return ObjectKind.Buildings;
		}
		if (name == null)
		{
			return ObjectKind.Other;
		}
		if (PieceNames.Contains(name) || Ruin.IsMatch(name)) return ObjectKind.Ruins;
		if (Pickable.IsMatch(name)) return ObjectKind.Pickables;
		if (Tree.IsMatch(name)) return ObjectKind.Trees;
		if (Tomb.IsMatch(name)) return ObjectKind.Other;
		if (Ore.IsMatch(name)) return ObjectKind.Ore;
		if (Rock.IsMatch(name)) return ObjectKind.Rocks;
		if (Bush.IsMatch(name)) return ObjectKind.Bushes;
		return ObjectKind.Other;
	}
}
