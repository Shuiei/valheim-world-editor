using System.Numerics;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Blueprints: copies kept as files (in the web editor's format), pasted, exported and imported.
public class BlueprintTests
{
	private static CopyData Sample() => new()
	{
		W = 3, H = 2,
		Rel = new[] { 0f, 0.5f, float.NaN, -1.25f, 2, 0 },
		Wt = new[] { 1f, 1, 0, 0.5f, 1, 1 },
		Pnt = new[] { -1f, -1, -1, -1, 1, 0, 0, 1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 0, 1, 1, -1, -1, -1, -1 },
		Objects = new() { new(StableHash.Of("woodwall"), "woodwall", 1, -0.5f, 0.25f, new Vector3(0, 90, 0), 0, 42), new(StableHash.Of("Beech1"), "Beech1", -1, 0, 0, Vector3.Zero, 1.1f, null, Follow: true) },
		Poly = new() { new(-2, -1), new(2, -1), new(2, 1), new(-2, 1) },
		Name = "Sample",
	};

	[Fact]
	public void ACopyGoesToJsonAndBack()
	{
		var c = Sample();
		var back = CopyFormat.FromJson(CopyFormat.ToJson(c));
		Assert.Equal(c.W, back.W);
		Assert.True(float.IsNaN(back.Rel[2]));
		Assert.Equal(-1.25f, back.Rel[3], 3);
		Assert.Equal(0.5f, back.Wt[3], 2);
		Assert.Equal(-1, back.Pnt[0]);
		Assert.Equal(1, back.Pnt[4], 2);
		Assert.Equal(c.Objects, back.Objects);
		Assert.Equal(c.Poly, back.Poly);
		// Elsewhere, the source ids are dropped.
		Assert.All(CopyFormat.FromJson(CopyFormat.ToJson(c), keepSources: false).Objects, o => Assert.Null(o.SourceId));
		Assert.StartsWith("data:image/png;base64,", CopyFormat.Thumb(c));
	}

	[Fact]
	public void TheWebEditorsBlueprintsAreRead()
	{
		// As the web editor's encodeClip writes it.
		var json = JsonNode.Parse("""{"w":1,"h":1,"rel":[-32768],"wt":[0],"pnt":[-255,-255,-255,-255],"objects":[{"name":"stone_wall_2x1","dx":0.5,"dz":0,"dy":1,"rx":0,"ry":180,"rz":0,"scale":0,"sourceId":null}],"poly":[{"gx":-1,"gz":-1},{"gx":1,"gz":-1},{"gx":1,"gz":1}]}""")!.AsObject();
		var c = CopyFormat.FromJson(json, "Wall");
		var o = Assert.Single(c.Objects);
		Assert.Equal(StableHash.Of("stone_wall_2x1"), o.Prefab);
		Assert.Equal(180, o.Rotation.Y);
		Assert.False(o.Follow);
		Assert.Equal(3, c.Poly.Count);
		Assert.Equal("Wall", c.Name);
	}

	[AvaloniaFact]
	public async Task SavedListedPastedExportedAndImported()
	{
		string dir = Path.Combine(Path.GetTempPath(), "vwe-bp-" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
			w.Show();
			var s = EditTests.Flat(2);
			w.View.Show(s.Scene, null);
			w.Edit(s);
			var bp = w.Blueprints;
			bp.Store = new BlueprintStore(dir);
			bp.AskName = _ => Task.FromResult<string?>("Gate");
			bp.Confirm = _ => Task.FromResult(true);
			Assert.Null(await bp.Save());
			Assert.StartsWith("Copy something first", w.MessageText.Text);
			w.View.Paste.Clip = Sample();
			Assert.Equal("Gate", await bp.Save());
			var listed = Assert.Single(bp.Store.List());
			Assert.Equal((3, 2, 2, true), (listed.W, listed.H, listed.Objects, listed.Ground));
			Assert.StartsWith("data:image/png", listed.Thumb);
			bp.Toggle(true);
			Assert.Single(bp.List.Children);
			// Pasting it: on the clipboard, in the Paste tool.
			w.View.Paste.Clip = null;
			bp.Paste(listed.Id);
			Assert.Equal("Gate", w.View.Paste.Clip!.Name);
			Assert.Equal(ToolMode.Paste, w.View.Mode);
			// For PlanBuild, and back in.
			string file = bp.Export(listed.Id, "blueprint")!;
			Assert.Contains("woodwall", File.ReadAllText(file));
			Assert.Equal("Gate (2)", bp.Import(file));
			var imported = bp.Store.List().Single(b => b.Name == "Gate (2)");
			Assert.Equal("PlanBuild", imported.Source);
			Assert.Equal(2, imported.Objects);
		}
		finally
		{
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, recursive: true);
			}
		}
	}
}
