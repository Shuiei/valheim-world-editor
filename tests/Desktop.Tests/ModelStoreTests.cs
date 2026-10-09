using System.Numerics;
using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The copied game models, read from a tiny store made here: a model's parts and scale, a mesh's
// vertices and triangles (turned for the mirrored z), a material's colour and options with their
// defaults, a texture's pixels, and everything missing or broken giving nothing instead of failing.
public sealed class ModelStoreTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-models-" + Guid.NewGuid().ToString("N")[..8]);

	public ModelStoreTests()
	{
		Directory.CreateDirectory(Path.Combine(_dir, "pieces"));
		Directory.CreateDirectory(Path.Combine(_dir, "meshes"));
		Directory.CreateDirectory(Path.Combine(_dir, "tex"));
		File.WriteAllText(Path.Combine(_dir, "objects.json"), "{\"123\": \"Thing\", \"not a number\": \"x\"}");
		File.WriteAllText(Path.Combine(_dir, "meshinfo.json"), "{\"m1\": {\"v\": 3, \"sub\": [3]}, \"empty\": {\"v\": 0, \"sub\": []}}");
		File.WriteAllText(Path.Combine(_dir, "materials.json"), "{\"mat\": {\"color\": [1, 0.5, 0.5, 0.8], \"cutoff\": 0.3, \"uv\": [2, 3, 0.1, 0.2], \"doubleSided\": true, \"map\": \"t.png\"}, \"glass\": {\"transparent\": true}}");
		File.WriteAllText(Path.Combine(_dir, "pieces", "Thing.json"), "{\"parts\": [{\"m\": [1,0,0,0, 0,1,0,0, 0,0,1,0, 5,6,7,1], \"mesh\": \"m1\", \"sub\": 0, \"mat\": \"mat\"}], \"rootScale\": [2, 2, 2]}");
		File.WriteAllText(Path.Combine(_dir, "pieces", "Empty.json"), "{\"parts\": []}");
		// Three vertices of 8 floats (position, normal, uv), then one triangle.
		var v = new float[] { 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 2, 3, 0, 1, 0, 0, 1 };
		var bytes = new byte[v.Length * 4 + 12];
		Buffer.BlockCopy(v, 0, bytes, 0, v.Length * 4);
		Buffer.BlockCopy(new uint[] { 0, 1, 2 }, 0, bytes, v.Length * 4, 12);
		File.WriteAllBytes(Path.Combine(_dir, "meshes", "m1.bin"), bytes);
		File.WriteAllBytes(Path.Combine(_dir, "meshes", "empty.bin"), Array.Empty<byte>());
		using (var bmp = new SKBitmap(2, 2))
		{
			bmp.SetPixel(0, 0, SKColors.Red);
			using var f = File.Create(Path.Combine(_dir, "tex", "t.png"));
			bmp.Encode(f, SKEncodedImageFormat.Png, 100);
		}
		File.WriteAllText(Path.Combine(_dir, "tex", "broken.png"), "not a picture");
	}

	public void Dispose() => Directory.Delete(_dir, true);

	[Fact]
	public void AModelHasItsPartsAndScale()
	{
		var store = new ModelStore(_dir);
		Assert.Equal("Thing", store.NameOf(123));
		var m = store.LoadModel("Thing")!;
		var part = Assert.Single(m.Parts);
		Assert.Equal("m1", part.Mesh);
		Assert.Equal("mat", part.Material);
		Assert.Equal(new Vector3(5, 6, 7), part.Matrix.Translation);
		Assert.Equal(new Vector3(2), m.RootScale);
		// Read once.
		Assert.Same(m, store.LoadModel("Thing"));
		Assert.Null(store.LoadModel("Missing"));
		Assert.Null(store.LoadModel("Empty"));
	}

	[Fact]
	public void AMeshIsTurnedForTheMirroredZ()
	{
		var store = new ModelStore(_dir);
		var mesh = store.LoadMesh("m1")!;
		Assert.Equal(24, mesh.Vertices.Length);
		Assert.Equal(new uint[] { 0, 2, 1 }, mesh.Submeshes[0]);
		Assert.Equal((new Vector3(0, 0, 0), new Vector3(1, 2, 3)), mesh.Bounds);
		Assert.Null(store.LoadMesh("unknown"));
		Assert.Equal((Vector3.Zero, Vector3.Zero), store.LoadMesh("empty")!.Bounds);
	}

	// A mesh on the graphics card is let go from memory: its box is kept, and it is read again if asked.
	[Fact]
	public void AForgottenMeshKeepsItsBoxAndIsReadAgain()
	{
		var store = new ModelStore(_dir);
		Assert.Null(store.BoundsOf("m1"));
		var first = store.LoadMesh("m1")!;
		store.Forget("m1");
		Assert.Equal(first.Bounds, store.BoundsOf("m1"));
		var again = store.LoadMesh("m1")!;
		Assert.NotSame(first, again);
		Assert.Equal(first.Vertices, again.Vertices);
	}

	[Fact]
	public void MaterialsHaveTheirOptionsOrDefaults()
	{
		var store = new ModelStore(_dir);
		var mat = store.Material("mat");
		Assert.Equal(1, mat.Color.X, 4);
		Assert.Equal(MathF.Pow(0.5f, 2.2f), mat.Color.Y, 4);
		Assert.Equal(0.8f, mat.Color.W, 4);
		Assert.Equal(0.3f, mat.Cutoff, 4);
		Assert.True(mat.DoubleSided);
		Assert.Equal("t.png", mat.Map);
		Assert.Equal(new Vector4(2, 3, 0.1f, 0.2f), mat.UvTransform);
		Assert.Equal(0.5f, store.Material("glass").Cutoff, 4);
		var none = store.Material("unknown");
		Assert.Equal(Vector4.One, none.Color);
		Assert.Equal(0, none.Cutoff);
		Assert.Null(none.Map);
	}

	[Fact]
	public void TexturesAreReadOrMissing()
	{
		var store = new ModelStore(_dir);
		var t = store.LoadTexture("t.png")!;
		Assert.Equal((2, 2), (t.Width, t.Height));
		Assert.Equal(255, t.Rgba[0]);
		Assert.Null(store.LoadTexture("broken.png"));
		Assert.Null(store.LoadTexture("missing.png"));
	}
}
