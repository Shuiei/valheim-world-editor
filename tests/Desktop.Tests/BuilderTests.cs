using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Built by, and making pieces player built (the test world's chest has no builder).
[Collection("World files")]
public class BuilderTests
{
	[AvaloniaFact]
	public void PiecesWithoutABuilderGetTheChosenOne()
	{
		string dir = EditTests.CopyFixture();
		long saved = WorldSave.Builder;
		try
		{
			var world = WorldSave.Load(dir);
			var at = world.Objects.First(o => o.Prefab == StableHash.Of("piece_chest_wood")).Position;
			var scene = WorldScene.Load(dir, (int)MathF.Floor((at.X + 32) / 64), (int)MathF.Floor((at.Z + 32) / 64), 1);
			var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
			w.Show();
			w.View.Show(scene, null);
			w.Edit(scene.Session!);
			// The list: the world's builders and players, another id, nobody.
			Assert.Equal(WorldSave.Builder, w.BuilderIds[w.BuilderBox.SelectedIndex]);
			Assert.Equal(0, w.BuilderIds[^1]);
			w.BuilderBox.SelectedIndex = w.BuilderIds.Count - 1;
			Avalonia.Threading.Dispatcher.UIThread.RunJobs();
			Assert.Equal(0, WorldSave.Builder);
			int chest = scene.Things.FindIndex(t => t.Prefab == StableHash.Of("piece_chest_wood"));
			w.Tools.ChooseSelect();
			w.View.Select(new[] { chest });
			w.Claim();
			Assert.StartsWith("Built by is set to Nobody", w.MessageText.Text);
			// Someone else: player 12345.
			w.AskPlayerId = () => Task.FromResult<string?>("12345");
			w.BuilderBox.SelectedIndex = w.BuilderIds.IndexOf(-1);
			Avalonia.Threading.Dispatcher.UIThread.RunJobs();
			Assert.Equal(12345, WorldSave.Builder);
			Assert.Equal(12345, w.PlacePanel.Memory.Builders[world.Name]);
			w.View.Select(new[] { chest });
			w.Claim();
			Assert.StartsWith("1 piece(s) are now player built", w.MessageText.Text);
			var copy = scene.Things[^1];
			Assert.True(copy.Piece);
			var z = ZdoData.Parse(ObjectData.Bytes(scene.World, scene.Session!.Edits, copy.Id)!);
			Assert.Equal(12345, z.LongList.First(l => l.Key == Builders.CreatorKey).Value);
			// Again: it has a builder now.
			w.View.Select(new[] { scene.Things.Count - 1 });
			w.Claim();
			Assert.StartsWith("Nothing to change", w.MessageText.Text);
		}
		finally
		{
			WorldSave.Builder = saved;
			Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
		}
	}
}
