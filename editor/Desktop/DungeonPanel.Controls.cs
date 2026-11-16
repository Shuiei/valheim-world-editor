using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The panel's controls: the dungeon list, the room list, its filter and the edit buttons.
/// </summary>
public sealed partial class DungeonPanel
{
    /// <summary>Which of the area's dungeons is edited.</summary>
    internal ComboBox DungeonBox { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
    /// <summary>The camera into the dungeon (see <see cref="Inside"/>); worded as the generator's for the dungeon it placed.</summary>
    internal Button GoInside { get; } = new() { Content = Lang.T("dungeon.label.toDungeon"), FontSize = 12 };
    /// <summary>The camera back down to the dungeon's entrance on the ground (see <see cref="Outside"/>).</summary>
    internal Button GoOutside { get; } = new() { Content = Lang.T("dungeon.label.toEntrance"), FontSize = 12 };
    /// <summary>Build with the hammer's pieces in this dungeon (<see cref="BuildAsked"/>).</summary>
    internal Button BuildHere { get; } = new() { Content = Lang.T("dungeon.label.buildHere"), FontSize = 12 };
    /// <summary>Build here was clicked: the window switches to building in the dungeon.</summary>
    public event Action? BuildAsked;
    /// <summary>Filters the rooms by name.</summary>
    internal TextBox Filter { get; } = new() { PlaceholderText = Lang.T("dungeon.hint.findRoom"), FontSize = 12 };
    /// <summary>The rooms the dungeon can take; the one chosen is added at the next green opening clicked.</summary>
    internal ListBox Rooms { get; } = new() { MaxHeight = 260, FontSize = 12 };
    /// <summary>Turns the room chosen (R).</summary>
    internal Button TurnButton { get; } = new() { Content = Lang.T("dungeon.label.turn"), FontSize = 12 };
    /// <summary>Closes the dungeon's open ends; enabled while it has some.</summary>
    internal Button CloseEnds { get; } = new() { Content = Lang.T("dungeon.label.closeEnds"), FontSize = 12 };
    /// <summary>Deletes the selected room (Del); enabled while one is selected.</summary>
    internal Button DeleteButton { get; } = new() { Content = Lang.T("dungeon.label.deleteRoom"), FontSize = 12 };
    /// <summary>Rooms, open ends, overlaps and problems of the dungeon, and of the room about to be added.</summary>
    internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>Rooms added with what the game puts in them.</summary>
    internal CheckBox Contents { get; } = new() { Content = Lang.T("dungeon.label.contents"), IsChecked = true, FontSize = 12 };
    /// <summary>Rooms added with doors where the game might put them.</summary>
    internal CheckBox Doors { get; } = new() { Content = Lang.T("dungeon.label.doors"), IsChecked = true, FontSize = 12 };
    /// <summary>Shown when the area has no dungeon of rooms (see <see cref="ShowPiecesPlaced"/>).</summary>
    internal TextBlock Empty { get; } = new()
    {
        FontSize = 12,
        Foreground = Ui.Muted,
        TextWrapping = TextWrapping.Wrap,
        Text = Lang.T("dungeon.hint.none")
    };
    /// <summary>The dungeon rows, shown when the area has a dungeon.</summary>
    private readonly StackPanel _body;
    /// <summary>The dungeons on <see cref="DungeonBox"/>, in its order.</summary>
    private List<DungeonRooms.Dungeon> _shown = [];
    /// <summary>While the lists are being filled: their selection changes are not the user's.</summary>
    private bool _filling;
}
