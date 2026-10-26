using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>
    /// Placed locations (villages, trader, dungeons...): their terrain flattening is applied live by the game and is
    /// not part of the saved terrain data.
    /// </summary>
    public List<(Vector3 Position, int Location)> Locations { get; } = [];

    /// <summary>The ground discs the editor placed for No limit ground (Uplift): invisible location proxies.</summary>
    public List<(int Id, Vector3 Position)> Discs { get; } = [];

    /// <summary>
    /// Every placed object that can modify terrain while the game runs: locations and the prefabs in ModifierPrefabs.
    /// Rotation is Euler degrees, as stored.
    /// </summary>
    public List<PlacedObject> Placed { get; } = [];

    /// <summary>Objects that are tamed creatures (by id).</summary>
    public HashSet<int> Tamed { get; } = [];

    /// <summary>
    /// The objects mods link into one (by id; App.ObjectLinks, by the rules in use as the world was read): what their
    /// data says of their place in their group, and their turn as stored (a group's root may lean).
    /// </summary>
    public Dictionary<int, (ObjectLink Link, Vector3 Rotation)> Links { get; } = [];

    /// <summary>
    /// Player-built pieces (objects with a creator): prefab, position and rotation (Unity Euler angles in degrees, as
    /// stored: a piece turned by a mod or pasted from a blueprint may lean, not only turn about y).
    /// </summary>
    public List<(int Id, int Prefab, Vector3 Position, Vector3 Rotation)> Pieces { get; } = [];

    /// <summary>Pieces per builder (the "creator" player id).</summary>
    public Dictionary<long, int> Creators { get; } = [];

    /// <summary>Player names by player id, from the objects that store both.</summary>
    public Dictionary<long, string> PlayerNames { get; } = [];

    /// <summary>
    /// The player id written as "creator" on new pieces (things built with the hammer, hoe, cultivator or serving
    /// tray), so the game treats them as player built. Chosen in the editor; 0: not set.
    /// </summary>
    public static long Builder { get; set; }

    /// <summary>Int data key ItemDrop.MakePiece sets on items set out as pieces.</summary>
    private static readonly int PieceKey = StableHash.Of("piece");

    /// <summary>The player who built the most pieces in this world (0 if nobody did).</summary>
    public long TopBuilder => Creators.Count == 0 ? 0 : Creators.MaxBy(c => c.Value).Key;

    /// <summary>Every object in the save by id (its order in the save).</summary>
    public List<ObjectRef> ObjectRefs { get; } = [];

    /// <summary>First object of each prefab: the template new objects of that prefab are copied from.</summary>
    public Dictionary<int, int> Templates { get; } = [];
    /// <summary>
    /// Counts each prefab added to <see cref="Templates"/> (a live merge brings new kinds), for lists worked out from
    /// them to be worked out again.
    /// </summary>
    public int TemplatesGeneration { get; private set; }
}
