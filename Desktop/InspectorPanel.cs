using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The object inspector, like the web editor's (editor/inspector.js; MCEdit's NBT editor): everything the
/// selected object holds in the save, by name, and editing it: sign text, portal tag, chest contents,
/// ward permissions, crop timers… Applying replaces the object by a copy with the new data, one undo step.
/// </summary>
public sealed class InspectorPanel
{
    /// <summary>
    /// The plain label of a data name players meet most, in the user's language; null for other names (shown as they
    /// are).
    /// </summary>
    /// <param name="name">The data name (its key's name).</param>
    private static string? PlainLabel(string name) => name switch
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

    /// <summary>What a section of the object data holds, in the user's language; null for bytes.</summary>
    /// <param name="section">The section, as ObjectData names it.</param>
    private static string? SectionName(string section) => section switch
    {
        "strings" => Lang.T("inspect.section.strings"),
        "ints" => Lang.T("inspect.section.ints"),
        "floats" => Lang.T("inspect.section.floats"),
        "longs" => Lang.T("inspect.section.longs"),
        "vec3" => Lang.T("inspect.section.vec3"),
        "quats" => Lang.T("inspect.section.quats"),
        _ => null,
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

    /// <summary>The card, hidden until an object is opened.</summary>
    /// <param name="session">Gives the current edit session (null when no world is open).</param>
    public InspectorPanel(Func<EditSession?> session)
    {
        _session = session;
        var close = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(6, 0) }.Tip("card.close");
        close.Click += (_, _) => Close();
        AddSection.Tip("inspect.addSection");
        AddKey.Tip("inspect.addKey");
        AddValue.Tip("inspect.addValue");
        AddButton.Tip("inspect.add");
        ApplyButton.Tip("inspect.apply");
        RevertButton.Tip("inspect.revert");
        AddButton.Click += (_, _) =>
        {
            string key = AddKey.Text?.Trim() ?? "";
            if (key == "")
            {
                Message?.Invoke(Said.Of("inspect.msg.typeName"));
                return;
            }
            if (ReadOnlyKey(key))
            {
                Message?.Invoke(Said.Of("inspect.msg.readOnly", key));
                return;
            }
            // One value a key: a second would be set over the first (or over the one in the list) on Apply.
            string section = Sections[Math.Max(0, AddSection.SelectedIndex)];
            int hash = ZdoKeys.Parse(key);
            if (FieldList.Exists(f => f.Section == section && f.Key == hash) || Added.Exists(a => a.Section == section && ZdoKeys.Parse(a.Key) == hash))
            {
                Message?.Invoke(Said.Of("inspect.msg.already", key));
                return;
            }
            Added.Add((section, key, AddValue.Text ?? ""));
            AddKey.Text = AddValue.Text = "";
            Render();
        };
        RevertButton.Click += (_, _) => Reset();
        ApplyButton.Click += async (_, _) => await Apply();
        Card = Ui.AsCard(new Border
        {
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            IsVisible = false,
            Child = new ScrollViewer
            {
                MaxHeight = 820,
                Content = new StackPanel
                {
                    Width = 380,
                    Spacing = 6,
                    Children =
                    {
                        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _title, Ui.Col(close, 1) } },
                        _where,
                        ItemsBox,
                        new TextBlock { Text = Lang.T("inspect.head.data"), FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 6, 0, 0) },
                        Fields,
                        new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,*,Auto"), ColumnSpacing = 4, Children = { AddSection, Ui.Col(AddKey, 1), Ui.Col(AddValue, 2), Ui.Col(AddButton, 3) } },
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ApplyButton, RevertButton } },
                        new TextBlock
                        {
                            Text = Lang.T("inspect.hint.apply"),
                            FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
            },
        });
    }

    /// <summary>True while the card is shown.</summary>
    public bool IsOpen => Card.IsVisible;

    /// <summary>Hides the card and forgets the object.</summary>
    public void Close()
    {
        Card.IsVisible = false;
        Index = null;
        Closed?.Invoke();
    }
    /// <summary>Raised when the card is closed.</summary>
    public event Action? Closed;

    /// <summary>Opens on a thing of the scene; false when its data cannot be read.</summary>
    public bool Open(int index)
    {
        var s = _session();
        if (s == null || s.Scene.World == null)
        {
            return false;
        }
        var t = s.Scene.Things[index];
        byte[]? bytes;
        try
        {
            bytes = ObjectData.Bytes(s.Scene.World, s.Edits, t.Id);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            bytes = null;
        }
        if (bytes == null)
        {
            Message?.Invoke(Said.Of("inspect.msg.cannotRead"));
            return false;
        }
        Index = index;
        _bytes = bytes;
        _z = ZdoData.Parse(bytes);
        Reset();
        Card.IsVisible = true;
        return true;
    }

    /// <summary>A float as text that reads back to the same value, whatever the culture.</summary>
    private static string S(float v) => v.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Back to the object's data as opened: its values, contents and stand, nothing changed.</summary>
    private void Reset()
    {
        var z = _z!;
        var list = new List<Field>();
        void Add(string section, int key, string value, string? note = null) => list.Add(new Field { Section = section, Key = key, Name = ZdoKeys.NameOf(key), Value = value, Now = value, Note = note });
        foreach (var (k, v) in z.FloatList)
        {
            Add("floats", k, S(v));
        }

        foreach (var (k, v) in z.Vec3List)
        {
            Add("vec3", k, $"{S(v.X)} {S(v.Y)} {S(v.Z)}");
        }

        foreach (var (k, v) in z.QuatList)
        {
            Add("quats", k, $"{S(v.X)} {S(v.Y)} {S(v.Z)} {S(v.W)}");
        }
        // Ints that are prefab or item hashes (item stands, cooking stations…) get the name beside them.
        foreach (var (k, v) in z.IntList)
        {
            Add("ints", k, v.ToString(CultureInfo.InvariantCulture), PrefabCatalog.NameOf(v));
        }

        foreach (var (k, v) in z.LongList)
        {
            Add("longs", k, v.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var (k, v) in z.StringList)
        {
            Add("strings", k, v);
        }

        foreach (var (k, v) in z.ByteList)
        {
            Add("bytes", k, Convert.ToBase64String(v), Lang.T("inspect.bytes", v.Length));
        }

        FieldList = list;
        Added.Clear();
        var details = PrefabCatalog.Details(z.Prefab);
        byte[]? items = z.GetBytes(ObjectData.ItemsKey);
        Items = null;
        _inventoryError = null;
        _grid = (details?.ContainerW ?? 0, details?.ContainerH ?? 0);
        if (items != null || details?.ContainerW > 0)
        {
            try
            {
                var inv = items != null ? InventoryData.Read(items) : new InventoryData();
                Items = [.. inv.Items.Select(i => new ItemRow
                {
                    Name = PrefabCatalog.NameOf(i.Prefab) ?? i.Prefab.ToString(CultureInfo.InvariantCulture),
                    Stack = i.Stack,
                    Quality = i.Quality,
                    Durability = i.Durability,
                    X = i.X,
                    Y = i.Y,
                    Variant = i.Variant,
                    WorldLevel = i.WorldLevel,
                    CrafterId = i.CrafterId.ToString(CultureInfo.InvariantCulture),
                    CrafterName = i.CrafterName,
                    Equipped = i.Equipped,
                    PickedUp = i.PickedUp,
                    Cheated = i.Cheated,
                    CustomData = new(i.CustomData),
                })];
            }
            catch (Exception ex) when (CheckedReads.Damaged(ex))
            {
                _inventoryError = ex.Message;
                Items = [];
            }
        }
        Stand = null;
        _armourStand = false;
        _standError = null;
        if (StandData.IsItemStand(z.Prefab))
        {
            var (held, way) = StandData.ReadItemStand(z);
            Stand = [new StandRow { Name = held?.Item ?? "", Quality = held?.Quality ?? 1, Variant = held?.Variant ?? 0 }];
            StandWay = way;
        }
        else if (PrefabCatalog.ArmourSlotsOf(z.Prefab) is { } slots)
        {
            _armourStand = true;
            var (held, pose) = StandData.ReadArmourStand(z, slots.Length);
            Stand = [];
            for (int i = 0; i < slots.Length; i++)
            {
                // ArmorStand has each of its slots twice: one row per kind, and any slot that holds something.
                int slot = i;
                if (held[i] == null && Enumerable.Range(0, i).Any(j => slots[j].SequenceEqual(slots[slot])))
                {
                    continue;
                }
                Stand.Add(new StandRow { Slot = i, Types = slots[i], Name = held[i]?.Item ?? "", Quality = held[i]?.Quality ?? 1, Variant = held[i]?.Variant ?? 0 });
            }
            StandWay = pose;
        }
        _standAtOpen = StandKey();
        _itemsAtOpen = ItemsKey();
        _slot = null;
        _moving = false;
        Render();
    }

    /// <summary>The contents in a short text form, to tell whether they changed.</summary>
    private string ItemsKey() => Items == null ? "" : string.Join(";", Items.Select(i => $"{i.Name},{i.Stack},{i.Quality},{i.Durability},{i.X},{i.Y}"));

    /// <summary>The stand's items and orientation or pose in a short text form, to tell whether they changed.</summary>
    private string StandKey() => Stand == null ? "" : string.Join(";", Stand.Select(r => $"{r.Name},{r.Quality},{r.Variant}")) + "|" + StandWay.ToString(CultureInfo.InvariantCulture);

    /// <summary>A value's label: the plain one, else a mod's manifest's, else its name, else its key's hash.</summary>
    private static string Label(Field f) => f.Name is string n ? PlainLabel(n) ?? ModManifests.DataOf(n)?.Label ?? n : $"#{f.Key}";

    /// <summary>
    /// A byte-array value as a running mod extension reads it (ModExtensions.Decode); null when none does (or for
    /// another kind of value).
    /// </summary>
    internal string? Decoded(Field f)
    {
        if (f.Section != "bytes" || _z is not { } z || z.GetBytes(f.Key) is not { } bytes)
        {
            return null;
        }
        var s = _session();
        WorldScene.Thing? t = Index is int ix && s != null && ix < s.Scene.Things.Count ? s.Scene.Things[ix] : null;
        var view = new ObjectView(PrefabCatalog.NameOf(z.Prefab) ?? "", t?.Position ?? z.Position, t?.Rotation ?? z.Rotation, z, t?.Scale ?? 1);
        return ModExtensions.Decode(f.Key, view, bytes);
    }

    /// <summary>Builds the whole card again from what is shown and changed.</summary>
    private void Render()
    {
        var z = _z!;
        var s = _session();
        string? name = PrefabCatalog.DisplayName(z.Prefab);
        _title.Text = name ?? Lang.T("inspect.prefab", z.Prefab);
        int id = Index is int ix && s != null && ix < s.Scene.Things.Count ? s.Scene.Things[ix].Id : 0;
        _where.Text = z.Connection != null
            ? Lang.T("inspect.whereConnected", id, z.Position.X, z.Position.Y, z.Position.Z, z.Rotation.Y)
            : Lang.T("inspect.where", id, z.Position.X, z.Position.Y, z.Position.Z, z.Rotation.Y);
        if (PrefabCatalog.SpawnOf(z.Prefab) is { } spawn)
        {
            _where.Text += "\n" + (spawn.Nest ? Lang.T("inspect.nest", spawn.Summary()) : Lang.T("inspect.spawnPoint", spawn.Summary()));
        }
        if (name != null && ModCatalogue.IsModded(name))
        {
            _where.Text += "\n" + (ModCatalogue.ModOf(name) is string mod ? Lang.T("inspect.fromMod", mod) : Lang.T("inspect.fromAMod"));
        }
        RenderItems();
        RenderStand();
        Fields.Children.Clear();
        if (FieldList.Count == 0 && Added.Count == 0)
        {
            Fields.Children.Add(new TextBlock { Text = Lang.T("inspect.noData"), FontSize = 11, Foreground = Ui.Muted });
        }
        foreach (var f in FieldList)
        {
            bool isItems = f.Section == "bytes" && f.Key == ObjectData.ItemsKey && Items != null && _inventoryError == null;
            // A mod's manifest can ask for a value to be shown only, and tell what it is.
            var note = ModManifests.DataOf(f.Name);
            bool ro = f.Section == "bytes";
            var box = new TextBox { Text = isItems ? Lang.T("inspect.seeContents") : ro ? f.Note : f.Now, IsReadOnly = ro || note?.ReadOnly == true, FontSize = 11, Opacity = f.Removed ? 0.5 : 1 }.Tip("inspect.value");
            if (note?.Help is string help)
            {
                ToolTip.SetTip(box, help);
            }
            box.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty && !ro)
                {
                    f.Now = box.Text ?? "";
                    Dirty();
                }
            };
            // A value a mod's manifest shows only cannot be removed either; nor can the contents while they are edited
            // above (Apply writes them again): empty them there instead.
            var rm = new Button { Content = f.Removed ? "↺" : "✕", FontSize = 11, Padding = new Thickness(4, 0), IsEnabled = note?.ReadOnly != true && !isItems };
            rm.Tip(f.Removed ? "inspect.keepValue" : "inspect.removeValue");
            rm.Click += (_, _) => { f.Removed = !f.Removed; Render(); };
            var label = new TextBlock { Text = Label(f), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextDecorations = f.Removed ? TextDecorations.Strikethrough : null };
            ToolTip.SetTip(label, $"{f.Name} · {SectionName(f.Section) ?? "data"} · key {f.Key}");
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,170,Auto"), ColumnSpacing = 4, Children = { label, Ui.Col(box, 1), Ui.Col(rm, 2) } };
            // Under a value: its note, or bytes as a mod's extension reads them.
            string? under = !ro ? f.Note : isItems ? null : Decoded(f);
            Fields.Children.Add(under != null
                ? new StackPanel { Children = { row, new TextBlock { Text = under, FontSize = 10, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap } } }
                : row);
        }
        foreach (var a in Added.ToList())
        {
            var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0) }.Tip("inspect.unadd");
            x.Click += (_, _) => { Added.Remove(a); Render(); };
            Fields.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,170,Auto"),
                ColumnSpacing = 4,
                Children = { new TextBlock { Text = Lang.T("inspect.added", PlainLabel(a.Key) ?? a.Key), FontSize = 11 }, Ui.Col(new TextBlock { Text = a.Value, FontSize = 11 }, 1), Ui.Col(x, 2) },
            });
        }
        Dirty();
    }

    /// <summary>
    /// The contents section: the slot grid (size known) and every item's details, with Add item and Tidy slots.
    /// </summary>
    private void RenderItems()
    {
        ItemsBox.Children.Clear();
        // A mod's manifest can ask for the contents (the "items" value) to be shown only.
        ItemsBox.IsEnabled = !ItemsReadOnly;
        if (Items == null)
        {
            return;
        }
        var (w, h) = _grid;
        ItemsBox.Children.Add(new TextBlock { Text = w > 0 ? Lang.T("inspect.head.contents", w, h, Items.Count) : Lang.T("inspect.head.contentsUnknown", Items.Count), FontSize = 10, Foreground = Ui.Muted });
        if (_inventoryError != null)
        {
            ItemsBox.Children.Add(new TextBlock { Text = Lang.T("inspect.error.contents", _inventoryError), FontSize = 11, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap });
            return;
        }
        if (w > 0 && h > 0)
        {
            SlotGrid(w, h);
        }
        var rows = new StackPanel { Spacing = 3 };
        var details = new Expander
        {
            Header = new TextBlock { Text = Lang.T("inspect.label.details"), FontSize = 11 },
            Content = rows,
            IsExpanded = w <= 0 || h <= 0 || Items.Any(i => i.X >= w || i.Y >= h),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0),
        }.Tip("inspect.details");
        ItemsBox.Children.Add(details);
        rows.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,54,44,50,40,40,26"),
            ColumnSpacing = 3,
            Children =
            {
                Head(Lang.T("inspect.data.item"), 0), Head(Lang.T("inspect.head.stack"), 1), Head(Lang.T("inspect.head.quality"), 2),
                Head(Lang.T("inspect.head.durability"), 3), Head(Lang.T("inspect.head.x"), 4), Head(Lang.T("inspect.head.y"), 5),
            },
        });
        foreach (var it in Items.ToList())
        {
            var name = new AutoCompleteBox { Text = it.Name, ItemsSource = PrefabCatalog.Items, FilterMode = AutoCompleteFilterMode.ContainsOrdinal, FontSize = 11, MinimumPrefixLength = 2 }.Tip("inspect.item");
            name.PropertyChanged += (_, e) => { if (e.Property == AutoCompleteBox.TextProperty) { it.Name = name.Text?.Trim() ?? ""; Dirty(); } };
            NumericUpDown Num(int v, int min, int max, Action<int> set, string tip)
            {
                var n = new NumericUpDown { Value = v, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", FontSize = 11, ShowButtonSpinner = false }.Tip(tip);
                n.ValueChanged += (_, e) => { set((int)(e.NewValue ?? min)); Dirty(); };
                return n;
            }
            // Durability points as the game saves them (not a percentage), fractions kept.
            var dur = new NumericUpDown
            {
                Value = float.IsFinite(it.Durability) ? (decimal)Math.Clamp(it.Durability, 0, MaxDurability) : 0,
                Minimum = 0,
                Maximum = MaxDurability,
                Increment = 1,
                FormatString = "0.##",
                FontSize = 11,
                ShowButtonSpinner = false,
            }.Tip("inspect.durability");
            dur.ValueChanged += (_, e) => { it.Durability = (float)(e.NewValue ?? 0); Dirty(); };
            var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0) };
            x.Tip("inspect.removeItem");
            x.Click += (_, _) => { Items.Remove(it); RenderItems(); Dirty(); };
            bool bad = w > 0 && (it.X >= w || it.Y >= h);
            rows.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,54,44,50,40,40,26"),
                ColumnSpacing = 3,
                Children =
                {
                    name,
                    Ui.Col(Num(it.Stack, 1, MaxStack, v => it.Stack = v, "inspect.count"), 1),
                    Ui.Col(Num(it.Quality, 1, 10, v => it.Quality = v, "inspect.quality"), 2),
                    Ui.Col(dur, 3),
                    Ui.Col(WithBorder(Num(it.X, 0, 255, v => it.X = v, "inspect.slotX"), bad), 4),
                    Ui.Col(WithBorder(Num(it.Y, 0, 255, v => it.Y = v, "inspect.slotY"), bad), 5),
                    Ui.Col(x, 6),
                },
            });
        }
        var add = new Button { Content = Lang.T("inspect.label.addItem"), FontSize = 11 }.Tip("inspect.addItem");
        add.Click += (_, _) =>
        {
            if (FreeSlot() is not var (fx, fy))
            {
                Message?.Invoke(Said.Of("inspect.msg.noFreeSlot"));
                return;
            }
            Items.Add(new ItemRow { X = fx, Y = fy });
            RenderItems();
            Dirty();
        };
        var tidy = new Button { Content = Lang.T("inspect.label.tidy"), FontSize = 11 };
        tidy.Tip("inspect.tidy");
        tidy.Click += (_, _) =>
        {
            int width = w > 0 ? w : 4;
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].X = i % width;
                Items[i].Y = i / width;
            }
            RenderItems();
            Dirty();
        };
        rows.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { add, tidy } });
    }

    /// <summary>
    /// The container's slots as in game, w × h: each shows its item and how many. Click one to choose
    /// what it holds (or empty it); Move, then another slot, moves the item there (or swaps the two).
    /// </summary>
    private void SlotGrid(int w, int h)
    {
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        for (int x = 0; x < w; x++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Min(46, 376.0 / w))));
        }
        for (int y = 0; y < h; y++)
        {
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(46)));
        }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var at = (x, y);
                var it = Items!.FirstOrDefault(i => i.X == x && i.Y == y);
                var cell = new Button
                {
                    Margin = new Thickness(1),
                    Padding = new Thickness(2),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                    Background = it != null ? Ui.Panel2 : Ui.Bg,
                    BorderThickness = new Thickness(_slot == at ? 2 : 1),
                    BorderBrush = _slot == at ? new SolidColorBrush(Ui.AccentColor) : Ui.Line,
                    Content = it == null ? null : new Grid
                    {
                        Children =
                        {
                            new TextBlock { Text = it.Name, FontSize = 9, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2 },
                            new TextBlock { Text = it.Stack > 1 ? it.Stack.ToString(CultureInfo.InvariantCulture) : "", FontSize = 11, FontWeight = FontWeight.SemiBold,
                                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom },
                        },
                    },
                }.Tip("inspect.slot");
                if (it != null)
                {
                    ToolTip.SetTip(cell, $"{it.Name} × {it.Stack} (slot {x + 1}, {y + 1})");
                }
                cell.Click += (_, _) =>
                {
                    if (_moving && _slot is var (fx, fy) && _slot != at)
                    {
                        var from = Items!.FirstOrDefault(i => i.X == fx && i.Y == fy);
                        var to = Items!.FirstOrDefault(i => i.X == at.x && i.Y == at.y);
                        if (from != null)
                        {
                            (from.X, from.Y) = at;
                        }
                        if (to != null)
                        {
                            (to.X, to.Y) = (fx, fy);
                        }
                        Dirty();
                    }
                    _moving = false;
                    _slot = at;
                    RenderItems();
                };
                Grid.SetColumn(cell, x);
                Grid.SetRow(cell, y);
                grid.Children.Add(cell);
            }
        }
        ItemsBox.Children.Add(grid);
        if (_slot is not var (sx, sy) || sx >= w || sy >= h)
        {
            ItemsBox.Children.Add(new TextBlock { Text = Lang.T("inspect.hint.clickSlot"), FontSize = 11, Foreground = Ui.Muted });
            return;
        }
        // The picked slot: its item (any of the game's), how many, how good.
        var items = Items!;
        var item = items.FirstOrDefault(i => i.X == sx && i.Y == sy);
        var pick = new AutoCompleteBox
        {
            Text = item?.Name ?? "",
            ItemsSource = PrefabCatalog.Items,
            FilterMode = AutoCompleteFilterMode.ContainsOrdinal,
            FontSize = 11,
            MinimumPrefixLength = 1,
            PlaceholderText = Lang.T("inspect.hint.findItem"),
        }.Tip("inspect.slotItem");
        void Chosen()
        {
            string name = pick.Text?.Trim() ?? "";
            string? known = PrefabCatalog.Items.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            // What the slot holds now: a pick and the box then losing focus (once the grid is drawn
            // again) both come here, and must not put the item in twice.
            var now = items.FirstOrDefault(i => i.X == sx && i.Y == sy);
            if (known == null || known == now?.Name)
            {
                return;
            }
            if (now == null)
            {
                items.Add(new ItemRow { Name = known, X = sx, Y = sy });
            }
            else
            {
                now.Name = known;
            }
            Dirty();
            RenderItems();
        }
        pick.SelectionChanged += (_, _) => Chosen();
        pick.LostFocus += (_, _) => Chosen();
        var count = new NumericUpDown { Value = item?.Stack ?? 1, Minimum = 1, Maximum = MaxStack, Increment = 1, FormatString = "0", FontSize = 11, Width = 90, IsEnabled = item != null }.Tip("inspect.count");
        count.ValueChanged += (_, e) =>
        {
            if (item != null)
            {
                item.Stack = (int)(e.NewValue ?? 1);
                Dirty();
            }
        };
        var quality = new NumericUpDown { Value = item?.Quality ?? 1, Minimum = 1, Maximum = 10, Increment = 1, FormatString = "0", FontSize = 11, Width = 70, IsEnabled = item != null }.Tip("inspect.quality");
        quality.ValueChanged += (_, e) =>
        {
            if (item != null)
            {
                item.Quality = (int)(e.NewValue ?? 1);
                Dirty();
            }
        };
        var empty = new Button { Content = Lang.T("inspect.label.empty"), FontSize = 11, IsEnabled = item != null }.Tip("inspect.emptySlot");
        empty.Click += (_, _) =>
        {
            items.Remove(item!);
            Dirty();
            RenderItems();
        };
        var move = new Button { Content = _moving ? Lang.T("inspect.label.clickSlot") : Lang.T("inspect.label.move"), FontSize = 11, IsEnabled = item != null }.Tip("inspect.moveSlot");
        move.Click += (_, _) =>
        {
            _moving = !_moving;
            RenderItems();
        };
        ItemsBox.Children.Add(new TextBlock { Text = Lang.T("inspect.head.slot", sx + 1, sy + 1), FontSize = 10, Foreground = Ui.Muted });
        ItemsBox.Children.Add(pick);
        ItemsBox.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = Lang.T("inspect.label.howMany"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, count,
                new TextBlock { Text = Lang.T("inspect.data.quality"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, quality, empty, move,
            },
        });
    }

    /// <summary>
    /// An item stand's item and orientation, or an armour stand's slots and pose; each list offers only
    /// what the game lets that stand or slot take.
    /// </summary>
    private void RenderStand()
    {
        if (Stand == null || _z == null)
        {
            return;
        }
        int prefab = _z.Prefab;
        ItemsBox.Children.Add(new TextBlock { Text = _armourStand ? Lang.T("inspect.head.wears") : Lang.T("inspect.head.holds"), FontSize = 10, Foreground = Ui.Muted });
        ItemsBox.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,44,44,26"),
            ColumnSpacing = 3,
            Children = { Head(Lang.T("inspect.data.item"), 0), Head(Lang.T("inspect.head.quality"), 1), Head(Lang.T("inspect.head.style"), 2) },
        });
        foreach (var row in Stand)
        {
            var choices = PrefabCatalog.Items.Where(n => _armourStand
                ? PrefabCatalog.ItemKindOf(StableHash.Of(n)) is { } k && StandData.FitsArmourSlot(row.Types, k)
                : StandData.ItemStandRefusal(prefab, n) == null).ToList();
            string slotName = row.Types.Length == 0 ? Lang.T("inspect.anything") : string.Join(", ", row.Types.Select(t => StandData.TypeWords(t).Shown).Distinct());
            var name = new AutoCompleteBox
            {
                Text = row.Name,
                ItemsSource = choices,
                FilterMode = AutoCompleteFilterMode.ContainsOrdinal,
                FontSize = 11,
                MinimumPrefixLength = 1,
                PlaceholderText = _armourStand ? slotName : Lang.T("inspect.hint.findItem"),
            }.Tip("inspect.standItem");
            name.PropertyChanged += (_, e) =>
            {
                if (e.Property == AutoCompleteBox.TextProperty)
                {
                    row.Name = name.Text?.Trim() ?? "";
                    Dirty();
                }
            };
            var empty = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0), IsEnabled = row.Name != "" }.Tip("inspect.emptyStand");
            empty.Click += (_, _) =>
            {
                row.Name = "";
                Dirty();
                RenderItems();
                RenderStand();
            };
            if (_armourStand)
            {
                ItemsBox.Children.Add(new TextBlock { Text = slotName, FontSize = 10, Foreground = Ui.Muted });
            }
            ItemsBox.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,44,44,26"),
                ColumnSpacing = 3,
                Children =
                {
                    name,
                    Ui.Col(StandNum(row.Quality, 1, 10, v => row.Quality = v, "inspect.quality"), 1),
                    Ui.Col(StandNum(row.Variant, 0, 99, v => row.Variant = v, "inspect.variant"), 2),
                    Ui.Col(empty, 3),
                },
            });
        }
        ItemsBox.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = _armourStand ? Lang.T("inspect.label.pose") : Lang.T("inspect.label.hangs"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
                StandNum(StandWay, 0, 99, v => StandWay = v, _armourStand ? "inspect.pose" : "inspect.orientation"),
            },
        });
        if (_standError != null)
        {
            ItemsBox.Children.Add(new TextBlock { Text = _standError.Shown, FontSize = 11, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap });
        }
    }

    /// <summary>A number box for the stand section that marks the card changed.</summary>
    private NumericUpDown StandNum(int v, int min, int max, Action<int> set, string tip)
    {
        var n = new NumericUpDown { Value = v, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", FontSize = 11, ShowButtonSpinner = false, Width = 60 }.Tip(tip);
        n.ValueChanged += (_, e) =>
        {
            set((int)(e.NewValue ?? min));
            Dirty();
        };
        return n;
    }

    /// <summary>A column heading.</summary>
    private static Control Head(string t, int col) => Ui.Col(new TextBlock { Text = t, FontSize = 10, Foreground = Ui.Muted }, col);

    /// <summary>Outlines a number box in orange when <paramref name="bad"/> (a slot outside the container).</summary>
    private static NumericUpDown WithBorder(NumericUpDown n, bool bad)
    {
        if (bad)
        {
            n.BorderBrush = Brushes.Orange;
        }
        return n;
    }

    /// <summary>The first free slot, row by row (size unknown: 4 wide, 2 rows: the smallest containers).</summary>
    internal (int X, int Y)? FreeSlot()
    {
        var (w, h) = _grid;
        int gw = w > 0 ? w : 4, gh = h > 0 ? h : Math.Max(2, Items!.Select(i => i.Y + 1).DefaultIfEmpty(0).Max());
        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                if (!Items!.Any(i => i.X == x && i.Y == y))
                {
                    return (x, y);
                }
            }
        }
        return null;
    }

    /// <summary>Whether a key typed to add (a name, or a hash as a number) is one a mod's manifest shows only.</summary>
    private static bool ReadOnlyKey(string key) => ModManifests.DataOf(ZdoKeys.NameOf(ZdoKeys.Parse(key)) ?? key)?.ReadOnly == true;

    /// <summary>Whether a mod's manifest asks for the contents to be shown only (its "items" value read-only).</summary>
    private static bool ItemsReadOnly => ModManifests.DataOf("items")?.ReadOnly == true;

    /// <summary>The changes made: values set (null: removed), and the contents when they changed.</summary>
    internal (List<FieldChange> Set, List<ItemUpload>? Inventory) Changes()
    {
        var set = new List<FieldChange>();
        foreach (var f in FieldList)
        {
            if (f.Removed)
            {
                set.Add(new FieldChange(f.Section, f.Key.ToString(CultureInfo.InvariantCulture), null));
            }
            else if (f.Now != f.Value && f.Section != "bytes")
            {
                set.Add(new FieldChange(f.Section, f.Key.ToString(CultureInfo.InvariantCulture), f.Now));
            }
        }
        // A value a mod's manifest shows only is never set, by name or by number.
        foreach (var (section, key, value) in Added.Where(a => !ReadOnlyKey(a.Key)))
        {
            set.Add(new FieldChange(section, key, value));
        }
        var inv = Items != null && _inventoryError == null && !ItemsReadOnly && ItemsKey() != _itemsAtOpen ? Items.Select(i => i.Upload()).ToList() : null;
        _standError = null;
        if (Stand != null && _z != null && StandKey() != _standAtOpen)
        {
            try
            {
                var held = Stand.Where(r => r.Name != "").Select(r => new StandData.Held(r.Name, r.Quality, r.Variant)).ToList();
                set.AddRange(_armourStand ? StandData.ForArmourStand(_z.Prefab, held, StandWay) : StandData.ForItemStand(_z.Prefab, held.FirstOrDefault(), StandWay));
            }
            catch (StandRefusalException ex)
            {
                _standError = ex.Reason;
            }
            catch (ArgumentException ex)
            {
                _standError = Said.Raw(ex.Message);
            }
        }
        return (set, inv);
    }

    /// <summary>Enables Apply and Revert when something changed.</summary>
    private void Dirty()
    {
        var (set, inv) = Changes();
        ApplyButton.IsEnabled = RevertButton.IsEnabled = set.Count > 0 || inv != null;
    }

    /// <summary>
    /// Writes the changes: the object is replaced by a copy with the new data, in one undo step, and the copy
    /// opened. Nothing is done when the object changed or went in the game since it was opened.
    /// </summary>
    internal async Task Apply()
    {
        if (_session() is not { } s || Index is not int index || _bytes == null)
        {
            return;
        }
        var (set, inv) = Changes();
        if (_standError != null)
        {
            Message?.Invoke(_standError);
            return;
        }
        var (w, h) = _grid;
        if (inv != null && w > 0 && inv.Any(i => i.X >= w || i.Y >= h)
            && !await Confirm(Lang.T("inspect.data.items"), Lang.T("inspect.ask.outside.text", w, h), Lang.T("inspect.ask.outside.yes")))
        {
            return;
        }
        ZdoData z;
        var notes = new List<string>();
        try
        {
            z = ObjectData.Edited(_bytes, set, inv, notes);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            Message?.Invoke(Said.Of("inspect.msg.couldNotChange", ex.Message));
            return;
        }
        // Another area open since (its things are others): nothing to change here.
        if (index >= s.Scene.Things.Count)
        {
            Message?.Invoke(Said.Of("inspect.msg.notInArea"));
            Close();
            return;
        }
        var t = s.Scene.Things[index];
        byte[]? now;
        try
        {
            now = t.Gone ? null : ObjectData.Bytes(s.Scene.World, s.Edits, t.Id);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Message?.Invoke(Said.Of("inspect.msg.cannotReadNow", ex.Message));
            return;
        }
        // Live: changed or removed in the game since it was opened (a player used the chest): the copy
        // would bring back its old data. Shown as it is now, to change again.
        if (t.Gone || !(now?.AsSpan().SequenceEqual(_bytes) ?? false))
        {
            // Gone offline: deleted, replaced or undone here; live, the game may have taken it too.
            var gone = s.IsLive ? Said.Of("inspect.msg.goneLive") : Said.Of("inspect.msg.gone");
            Message?.Invoke(t.Gone ? gone : Said.Of("inspect.msg.changedInGame"));
            if (t.Gone)
            {
                Close();
            }
            else
            {
                Open(index);
            }
            return;
        }
        var name = PrefabCatalog.DisplayName(z.Prefab) is string shown ? Said.Raw(shown) : Said.Of("inspect.object");
        var copies = s.Commit(Lang.T("inspect.undo.edited", name.Shown), null, [index], [(new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize()), t.Piece)]);
        // The notes come from ObjectData in English, each its own sentence.
        Message?.Invoke(Said.Of("inspect.msg.changed", name) + Said.Join(" ", notes.Select(n => Said.Raw(n + "."))));
        Open(copies[0]);
        Replaced?.Invoke(copies[0]);
    }
}
