namespace TerrainEditor.App;

/// <summary>
/// The kinds in the View panel: their order and their names in the language in use and in English.
/// </summary>
public static partial class ObjectKinds
{
    /// <summary>Every kind, in the View panel's order.</summary>
    public static readonly ObjectKind[] All = Enum.GetValues<ObjectKind>();

    /// <summary>The kind's name in the View panel, in the language in use.</summary>
    public static string Label(ObjectKind k) => Lang.T(LabelKey(k));

    /// <summary>The kind's name in the language in use and in English (for what Claude reads back).</summary>
    public static Said Name(ObjectKind k) => Said.Of(LabelKey(k));

    /// <summary>The catalogue key of the kind's name.</summary>
    private static string LabelKey(ObjectKind k) => k switch
    {
        ObjectKind.Buildings => "kind.label.buildings",
        ObjectKind.Ruins => "kind.label.ruins",
        ObjectKind.Furniture => "kind.label.furniture",
        ObjectKind.Trees => "kind.label.trees",
        ObjectKind.Rocks => "kind.label.rocks",
        ObjectKind.Ore => "kind.label.ore",
        ObjectKind.Bushes => "kind.label.bushes",
        ObjectKind.Pickables => "kind.label.pickables",
        ObjectKind.Remains => "kind.label.remains",
        ObjectKind.Animals => "kind.label.animals",
        ObjectKind.Creatures => "kind.label.creatures",
        ObjectKind.SpawnPoints => "kind.label.spawnPoints",
        ObjectKind.Nests => "kind.label.nests",
        ObjectKind.Runestones => "kind.label.runestones",
        ObjectKind.Dungeons => "kind.label.dungeons",
        ObjectKind.Effects => "kind.label.effects",
        _ => "kind.label.other",
    };

    /// <summary>Shown when the editor opens, as in the web editor's View panel (spoilers stay hidden).</summary>
    public static bool ShownAtFirst(ObjectKind k) => k is ObjectKind.Buildings or ObjectKind.Trees or ObjectKind.Rocks or ObjectKind.Animals or ObjectKind.Runestones or ObjectKind.Dungeons;
}
