using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The models read from the game's own bundles (BundleModels) against a copy the old Python exporter
// made of the same game (game-look/models): same parts, vertices, triangles, placement, materials and
// textures. Needs Valheim and that copy on this computer (skipped elsewhere, as in CI). Run alone:
// dotnet test tests/Desktop.Tests --filter Category=Game
[Trait("Category", "Game")]
public class GameFilesTests
{
	private static string LookModels => Path.Combine(AppSettings.UserDataDir, "game-look", "models");

	private static (ModelStore Bundles, ModelStore Copy)? Stores()
	{
		string? valheim = GameLook.FindValheim(null);
		if (valheim == null || !File.Exists(Path.Combine(LookModels, "meshinfo.json")))
		{
			return null;
		}
		var game = BundleModels.ForGame(valheim);
		return game == null ? null : (new ModelStore(game), new ModelStore(LookModels));
	}

	[Fact]
	public void ModelsReadFromTheGameAreTheOnesTheExporterCopied()
	{
		var stores = Stores();
		Assert.SkipUnless(stores != null, "needs Valheim and a game-look copy made by the old exporter");
		var (bundles, copy) = stores!.Value;
		var names = Directory.GetFiles(Path.Combine(LookModels, "pieces"), "*.json").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal).ToList();
		// A spread of every kind (pieces, objects, rooms), and the biggest room.
		var sample = names.Where((_, i) => i % 25 == 0).Append("morkhalla_entrance02").Append("piece_workbench").Distinct().ToList();
		var problems = new List<string>();
		int parts = 0, textures = 0;
		foreach (string name in sample)
		{
			var want = copy.LoadModel(name!);
			var got = bundles.LoadModel(name!);
			if (want == null)
			{
				continue;
			}
			if (got == null || got.Parts.Count != want.Parts.Count)
			{
				problems.Add($"{name}: {got?.Parts.Count ?? 0} parts, the copy {want.Parts.Count}");
				continue;
			}
			if (Vector3.Distance(got.RootScale, want.RootScale) > 1e-4f)
			{
				problems.Add($"{name}: root scale {got.RootScale}, the copy {want.RootScale}");
			}
			for (int i = 0; i < want.Parts.Count; i++)
			{
				var (g, w) = (got.Parts[i], want.Parts[i]);
				parts++;
				if (g.Sub != w.Sub || !Near(g.Matrix, w.Matrix, 2e-3f))
				{
					problems.Add($"{name} part {i}: sub {g.Sub}/{w.Sub}, matrix {g.Matrix} / {w.Matrix}");
				}
				var (gm, wm) = (bundles.LoadMesh(g.Mesh), copy.LoadMesh(w.Mesh));
				if (gm == null || wm == null || gm.Vertices.Length != wm.Vertices.Length || gm.Submeshes.Length != wm.Submeshes.Length)
				{
					problems.Add($"{name} part {i}: mesh {gm?.Vertices.Length / 8} vertices, the copy {wm?.Vertices.Length / 8}");
					continue;
				}
				float worst = 0;
				for (int k = 0; k < wm.Vertices.Length; k++)
				{
					worst = Math.Max(worst, Math.Abs(gm.Vertices[k] - wm.Vertices[k]));
				}
				if (worst > 1e-3f)
				{
					problems.Add($"{name} part {i}: vertices differ by up to {worst}");
				}
				if (!gm.Submeshes[g.Sub].SequenceEqual(wm.Submeshes[w.Sub]))
				{
					problems.Add($"{name} part {i}: triangles differ ({gm.Submeshes[g.Sub].Length} / {wm.Submeshes[w.Sub].Length} indices)");
				}
				var (gmat, wmat) = (bundles.Material(g.Material), copy.Material(w.Material));
				if (Vector4.Distance(gmat.Color, wmat.Color) > 2e-3f || gmat.Cutoff != wmat.Cutoff || gmat.DoubleSided != wmat.DoubleSided
					|| Vector4.Distance(gmat.UvTransform, wmat.UvTransform) > 1e-4f || (gmat.Map == null) != (wmat.Map == null))
				{
					problems.Add($"{name} part {i}: material {gmat} / {wmat}");
					continue;
				}
				if (gmat.Map != null && i < 3)
				{
					var (gt, wt) = (bundles.LoadTexture(gmat.Map), copy.LoadTexture(wmat.Map!));
					textures++;
					if (gt == null || wt == null || gt.Width != wt.Width || gt.Height != wt.Height)
					{
						problems.Add($"{name} part {i}: texture {gt?.Width}x{gt?.Height}, the copy {wt?.Width}x{wt?.Height}");
						continue;
					}
					// The copy was made from the full-size picture, scaled and saved as JPEG or PNG: close, not equal.
					// Only where it is seen (the hidden colour of cut-out pixels is filled in differently).
					double diff = 0;
					int seen = 0;
					for (int k = 0; k < gt.Rgba.Length; k += 4)
					{
						if (wt.Rgba[k + 3] < 128)
						{
							continue;
						}
						seen++;
						diff += Math.Abs(gt.Rgba[k] - wt.Rgba[k]) + Math.Abs(gt.Rgba[k + 1] - wt.Rgba[k + 1]) + Math.Abs(gt.Rgba[k + 2] - wt.Rgba[k + 2]);
					}
					diff /= Math.Max(1, seen * 3);
					if (diff > 12)
					{
						problems.Add($"{name} part {i}: texture colours differ by {diff:0.0} on average");
					}
				}
			}
		}
		Assert.True(problems.Count == 0, $"{sample.Count} models, {parts} parts, {textures} textures compared:\n" + string.Join("\n", problems.Take(40)));
		Assert.True(parts > 2000 && textures > 20, $"too little compared: {parts} parts, {textures} textures");
	}

	private static bool Near(Matrix4x4 a, Matrix4x4 b, float e)
	{
		for (int r = 0; r < 4; r++)
		{
			for (int c = 0; c < 4; c++)
			{
				if (Math.Abs(a[r, c] - b[r, c]) > e * Math.Max(1, Math.Abs(b[r, c])))
				{
					return false;
				}
			}
		}
		return true;
	}
}
