using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// The object inspector, like the web editor's (editor/inspector.js; MCEdit's NBT editor): everything the
/// selected object holds in the save, by name, and editing it: sign text, portal tag, chest contents,
/// ward permissions, crop timers… Applying replaces the object by a copy with the new data, one undo step.
/// </summary>
public sealed partial class InspectorPanel
{
    /// <summary>
    /// The plain label of a data name players meet most, in the user's language; null for other names (shown as they
    /// are).
    /// </summary>
    /// <param name="name">The data name (its key's name).</param>
    internal static string? PlainLabel(string name) => name switch
    {
        "text" => Lang.T("inspect.data.text"),
        "tag" => Lang.T("inspect.data.tag"),
        "creator" => Lang.T("inspect.data.creator"),
        "creatorName" => Lang.T("inspect.data.creatorName"),
        "author" => Lang.T("inspect.data.author"),
        "health" => Lang.T("inspect.data.health"),
        "plantTime" => Lang.T("inspect.data.plantTime"),
        "items" => Lang.T("inspect.data.items"),
        "permitted" => Lang.T("inspect.data.permitted"),
        "enabled" => Lang.T("inspect.data.enabled"),
        "item" => Lang.T("inspect.data.item"),
        "variant" => Lang.T("inspect.data.variant"),
        "quality" => Lang.T("inspect.data.quality"),
        "ownerName" => Lang.T("inspect.data.ownerName"),
        "owner" => Lang.T("inspect.data.owner"),
        "fuel" => Lang.T("inspect.data.fuel"),
        "StartTime" => Lang.T("inspect.data.startTime"),
        "level" => Lang.T("inspect.data.level"),
        "tamed" => Lang.T("inspect.data.tamed"),
        "support" => Lang.T("inspect.data.support"),
        "scale" => Lang.T("inspect.data.scale"),
        "picked" => Lang.T("inspect.data.picked"),
        "picked_time" => Lang.T("inspect.data.pickedTime"),
        "spawntime" => Lang.T("inspect.data.spawnTime"),
        "lastTime" => Lang.T("inspect.data.lastTime"),
        "accTime" => Lang.T("inspect.data.accTime"),
        "queued" => Lang.T("inspect.data.queued"),
        "state" => Lang.T("inspect.data.state"),
        _ => null,
    };

    /// <summary>The object data's sections (as ObjectData names them) the Add row offers, in its order.</summary>
    private static readonly string[] Sections = ["strings", "ints", "floats", "longs", "vec3", "quats"];

    /// <summary>What a section of the object data holds, in the user's language ("data" for bytes).</summary>
    /// <param name="section">The section, as ObjectData names it.</param>
    internal static string SectionName(string section) => section switch
    {
        "strings" => Lang.T("inspect.section.strings"),
        "ints" => Lang.T("inspect.section.ints"),
        "floats" => Lang.T("inspect.section.floats"),
        "longs" => Lang.T("inspect.section.longs"),
        "vec3" => Lang.T("inspect.section.vec3"),
        "quats" => Lang.T("inspect.section.quats"),
        _ => Lang.T("inspect.section.data"),
    };

    /// <summary>The floating card, hidden until an object is opened.</summary>
    public Control Card { get; }
    /// <summary>The current edit session, null when no world is open.</summary>
    private readonly Func<EditSession?> _session;
    /// <summary>The object's name.</summary>
    private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold };
    /// <summary>The object's id, place and turn.</summary>
    private readonly TextBlock _where = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>The contents and stand sections, rebuilt by <c>RenderItems</c> and <c>RenderStand</c>.</summary>
    internal StackPanel ItemsBox { get; } = new() { Spacing = 3 };
    /// <summary>One row per data value, rebuilt by <c>Render</c>.</summary>
    internal StackPanel Fields { get; } = new() { Spacing = 2 };
    /// <summary>The section of a value to add.</summary>
    internal ComboBox AddSection { get; } = new() { ItemsSource = Sections.Select(SectionName).ToList(), SelectedIndex = 0, FontSize = 11, Width = 100 };
    /// <summary>The name of a value to add.</summary>
    internal TextBox AddKey { get; } = new() { PlaceholderText = Lang.T("inspect.hint.addKey"), FontSize = 11 };
    /// <summary>The value to add.</summary>
    internal TextBox AddValue { get; } = new() { PlaceholderText = Lang.T("inspect.hint.addValue"), FontSize = 11 };
    /// <summary>Adds the value to <c>Added</c> (written on Apply).</summary>
    internal Button AddButton { get; } = new() { Content = Lang.T("common.add"), FontSize = 11 };
    /// <summary>Applies the changes (<c>Apply</c>); enabled only while there are some.</summary>
    internal Button ApplyButton { get; } = new() { Content = Lang.T("inspect.label.apply"), FontSize = 12, IsEnabled = false };
    /// <summary>Throws away the changes, back to the object's data as opened.</summary>
    internal Button RevertButton { get; } = new() { Content = Lang.T("inspect.label.revert"), FontSize = 12, IsEnabled = false };
    /// <summary>A status line to show (what happened, or why nothing did).</summary>
    public event Action<Said>? Message;
    /// <summary>After applying: the copy that replaced the object (to select it).</summary>
    public event Action<int>? Replaced;
    /// <summary>
    /// Asks a question before going on, with its title, its text and the button that goes on (the window shows it;
    /// true by default).
    /// </summary>
    internal Func<string, string, string, Task<bool>> Confirm { get; set; } = (_, _, _) => Task.FromResult(true);

    /// <summary>What is shown: the thing (index into the scene's things), null when closed.</summary>
    public int? Index { get; private set; }
    /// <summary>The object's data as parsed when opened.</summary>
    private ZdoData? _z;
    /// <summary>The object's data as read when opened; Apply compares it with the world's to see a live change.</summary>
    private byte[]? _bytes;
    /// <summary>A value of the object's data, as opened and as edited.</summary>
    internal sealed class Field
    {
        /// <summary>The data section ("floats", "vec3", "quats", "ints", "longs", "strings" or "bytes").</summary>
        public required string Section;
        /// <summary>The key's hash.</summary>
        public required int Key;
        /// <summary>The key's name when known, null otherwise.</summary>
        public required string? Name;
        /// <summary>The value as opened, as text.</summary>
        public required string Value;
        /// <summary>The value as edited, as text.</summary>
        public string Now = "";
        /// <summary>Shown beside the value: the prefab or item name of a hash, or the length of bytes.</summary>
        public string? Note;
        /// <summary>Taken out on Apply.</summary>
        public bool Removed;
    }
    /// <summary>The most items a stack may hold, as the save keeps them (the details row and the picked slot alike).</summary>
    internal const int MaxStack = 65535;

    /// <summary>The most durability points an item's box takes.</summary>
    private const int MaxDurability = 100000;

    /// <summary>The object's values, as built when opened.</summary>
    internal List<Field> FieldList { get; private set; } = [];
    /// <summary>Values to add, as typed (section, name, value).</summary>
    internal List<(string Section, string Key, string Value)> Added { get; } = [];
    /// <summary>An item in the container, as edited.</summary>
    internal sealed class ItemRow
    {
        /// <summary>The item's prefab name.</summary>
        public string Name = "Wood";
        /// <summary>How many; quality; slot (X across, Y down); style variant; the world level it was made at.</summary>
        public int Stack = 1, Quality = 1, X, Y, Variant, WorldLevel;
        /// <summary>Durability points, as the game saves them.</summary>
        public float Durability = 100;
        /// <summary>Who made it: player id (as text) and name.</summary>
        public string CrafterId = "0", CrafterName = "";
        /// <summary>The item's flags as the game keeps them (equipped, picked up, given by a cheat).</summary>
        public bool Equipped, PickedUp, Cheated;
        /// <summary>The key/value data mods and the game attach to the item, kept as it was.</summary>
        public Dictionary<string, string> CustomData = [];
        /// <summary>The item as ObjectData writes it.</summary>
        public ItemUpload Upload() => new(Name, null, Stack, Quality, Durability, X, Y, Variant, WorldLevel, CrafterId, CrafterName, Equipped, PickedUp, Cheated, CustomData);
    }
    /// <summary>The container's items; null when the object is not a container.</summary>
    internal List<ItemRow>? Items { get; private set; }

    /// <summary>
    /// A stand's items: an item stand's one item, or an armour stand's slots (one row per kind of slot,
    /// and any slot that holds something); the item stand's orientation or the armour stand's pose.
    /// </summary>
    internal sealed class StandRow
    {
        /// <summary>The armour stand's slot index (0 on an item stand).</summary>
        public int Slot;
        /// <summary>The item types the slot takes; empty for an item stand.</summary>
        public int[] Types = [];
        /// <summary>The item's prefab name, empty when the slot holds nothing.</summary>
        public string Name = "";
        /// <summary>The item's quality and style variant.</summary>
        public int Quality = 1, Variant;
    }
    /// <summary>The stand's rows; null when the object is not a stand.</summary>
    internal List<StandRow>? Stand { get; private set; }
    /// <summary>The item stand's orientation or the armour stand's pose.</summary>
    internal int StandWay { get; set; }
    /// <summary>True for an armour stand, false for an item stand.</summary>
    private bool _armourStand;
    /// <summary>The stand as opened (<c>StandKey</c>), to tell whether it changed.</summary>
    private string _standAtOpen = "";
    /// <summary>Why the stand's changes cannot be written (an item it does not take), null when they can.</summary>
    private Said? _standError;
    /// <summary>The contents as opened (<c>ItemsKey</c>), to tell whether they changed.</summary>
    private string _itemsAtOpen = "";
    /// <summary>The container's size in slots; 0 × 0 when unknown.</summary>
    private (int W, int H) _grid;
    /// <summary>The slot picked in the grid.</summary>
    private (int X, int Y)? _slot;
    /// <summary>The next slot clicked takes the picked slot's item.</summary>
    private bool _moving;
    /// <summary>Why the contents cannot be read, null when they can (they are then not edited).</summary>
    private string? _inventoryError;
}
