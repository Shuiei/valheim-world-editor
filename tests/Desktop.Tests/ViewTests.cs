using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The View panel: objects sorted into kinds like the web editor, and switches that hide them.
public class ViewTests
{
	[Theory]
	[InlineData("Beech1", false, ObjectKind.Trees)]
	[InlineData("FirTree_log", false, ObjectKind.Trees)]
	[InlineData("Rock_4", false, ObjectKind.Rocks)]
	[InlineData("MineRock_Copper", false, ObjectKind.Ore)]
	[InlineData("Pickable_Mushroom", false, ObjectKind.Pickables)]
	[InlineData("Bush01", false, ObjectKind.Bushes)]
	[InlineData("Player_tombstone", false, ObjectKind.Other)]
	[InlineData("piece_chest_wood", true, ObjectKind.Buildings)]
	[InlineData("woodwall", false, ObjectKind.Ruins)]
	[InlineData("goblin_woodwall_1m", false, ObjectKind.Ruins)]
	[InlineData(null, false, ObjectKind.Other)]
	public void KindsFollowTheWebEditor(string? name, bool built, ObjectKind want) => Assert.Equal(want, ObjectKinds.Of(name, built));

	[AvaloniaFact]
	public void SwitchesStartLikeTheWebEditorAndHideKinds()
	{
		var w = new MainWindow(load: false);
		w.Show();
		foreach (var k in ObjectKinds.All)
		{
			Assert.Equal(ObjectKinds.ShownAtFirst(k), w.KindBoxes[k].IsChecked);
			Assert.Equal(ObjectKinds.ShownAtFirst(k), w.View.IsShown(k));
		}
		w.KindBoxes[ObjectKind.Trees].IsChecked = false;
		Assert.False(w.View.IsShown(ObjectKind.Trees));
		w.KindBoxes[ObjectKind.Ore].IsChecked = true;
		Assert.True(w.View.IsShown(ObjectKind.Ore));
		w.WaterBox.IsChecked = false;
		Assert.False(w.View.ShowWater);
	}
}
