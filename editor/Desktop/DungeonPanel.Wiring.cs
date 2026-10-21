using Avalonia.Controls;

namespace TerrainEditor.Desktop;

/// <summary>
/// Hooks the panel's controls: their clicks, selections and edits drive the view's Dungeon tool.
/// </summary>
public sealed partial class DungeonPanel
{
    /// <summary>
    /// Sets the controls' tooltips and hooks their events.
    /// </summary>
    private void Wire()
    {
        DungeonBox.Tip("dungeon.which");
        GoInside.Tip("dungeon.inside");
        GoOutside.Tip("dungeon.outside");
        BuildHere.Tip("dungeon.build");
        BuildHere.Click += (_, _) => BuildAsked?.Invoke();
        CutSlider.Tip("dungeon.cut");
        Filter.Tip("dungeon.find");
        Rooms.Tip("dungeon.rooms");
        TurnButton.Tip("dungeon.turn");
        CloseEnds.Tip("dungeon.close");
        DeleteButton.Tip("dungeon.delete");
        Contents.Tip("dungeon.contents");
        Contents.IsCheckedChanged += (_, _) => Tool.WithContents = Contents.IsChecked == true;
        Doors.Tip("dungeon.doors");
        Doors.IsCheckedChanged += (_, _) => Tool.WithDoors = Doors.IsChecked == true;
        DungeonBox.SelectionChanged += (_, _) =>
        {
            if (!_filling && DungeonBox.SelectedIndex >= 0 && DungeonBox.SelectedIndex < _shown.Count)
            {
                Tool.Open(_shown[DungeonBox.SelectedIndex]);
                FillRooms();
            }
        };
        GoInside.Click += (_, _) => Inside();
        GoOutside.Click += (_, _) => Outside();
        CutSlider.ValueChanged += (_, e) => SetCut(e.NewValue);
        Filter.TextChanged += (_, _) => FillRooms();
        Rooms.SelectionChanged += (_, _) =>
        {
            if (!_filling)
            {
                Tool.Choose((Rooms.SelectedItem as ListBoxItem)?.Tag as string);
            }
        };
        TurnButton.Click += (_, _) => Tool.Turn();
        CloseEnds.Click += (_, _) => Tool.CloseOpenEnds();
        DeleteButton.Click += (_, _) => Tool.Delete();
        Tool.Changed += Refresh;
    }
}
