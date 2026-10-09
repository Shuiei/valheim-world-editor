namespace TerrainEditor.Desktop;

// Building in a dungeon (the Dungeon panel's Build here): the Workshop's Build (one piece at a time,
// where the hammer would put it, snapping) over the dungeon's rooms, whose floors and walls the cursor
// points at. Pieces resting on a room hold in game: its floor counts as ground for their support.
public partial class MainWindow
{
	private Kept? _dungeonKept;

	internal bool BuildingInDungeon => _dungeonKept != null;

	internal void BuildInDungeon(bool on)
	{
		var t = PlaceTool;
		if (on && _dungeonKept == null)
		{
			_dungeonKept = new Kept(t.Chosen.ToList(), t.Mode, t.OneAtATime, t.OneAtATimeByHand, t.RandomYaw, t.Tilt, t.SizeMin, t.SizeMax, t.Elevation, t.SnapTo, t.OnTop, t.Rotation, false);
			BuildPanel.Start();
			t.CutY = _view.CutY;
			Tools.ChooseMode(ToolMode.Place);
			_message.Text = "Building in the dungeon: pick a piece, then click on a room's floor or on a piece. Pieces on a room's floor hold in game.";
		}
		else if (!on && _dungeonKept is { } k)
		{
			_dungeonKept = null;
			t.Chosen.Clear();
			t.Chosen.AddRange(k.Chosen);
			(t.Mode, t.OneAtATime, t.OneAtATimeByHand, t.RandomYaw, t.Tilt, t.SizeMin, t.SizeMax, t.Elevation, t.SnapTo, t.OnTop, t.Rotation) =
				(k.Mode, k.OneAtATime, k.OneAtATimeByHand, k.RandomYaw, k.Tilt, k.SizeMin, k.SizeMax, k.Elevation, k.SnapTo, k.OnTop, k.Rotation);
			t.GridStep = 0;
			t.Building = false;
			t.HeightNudge = 0;
			t.AimRay = null;
			t.CutY = null;
			PlaceInput.TurnStep = null;
			t.Notify();
		}
	}
}
