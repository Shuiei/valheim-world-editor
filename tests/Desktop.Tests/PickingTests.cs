using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Picking: the ray through the mouse, against boxes and the ground.
public class PickingTests
{
	private static Matrix4x4 Camera(Vector3 eye, Vector3 target)
	{
		return Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY) * GlView.Perspective(MathF.PI / 3, 1.5f, 0.5f, 6000);
	}

	[Fact]
	public void TheRayThroughTheMiddleGoesToTheTarget()
	{
		var (o, d) = Picking.Ray(Camera(new Vector3(0, 10, 10), Vector3.Zero), 0, 0);
		var toTarget = Vector3.Normalize(Vector3.Zero - new Vector3(0, 10, 10));
		Assert.True(Vector3.Distance(d, toTarget) < 1e-3f, $"{d}");
		// It starts on the near plane, half a metre in front of the camera.
		Assert.True(MathF.Abs(Vector3.Distance(o, new Vector3(0, 10, 10)) - 0.5f) < 0.01f, $"{o}");
	}

	[Fact]
	public void BoxesAreHitWhereTheRayEnters()
	{
		var o = new Vector3(0, 0, 10);
		var d = new Vector3(0, 0, -1);
		Assert.Equal(9f, Picking.HitBox(o, d, new Vector3(-1, -1, -1), new Vector3(1, 1, 1))!.Value, 4);
		Assert.Null(Picking.HitBox(o, d, new Vector3(2, -1, -1), new Vector3(3, 1, 1)));
		Assert.Null(Picking.HitBox(o, -d, new Vector3(-1, -1, -1), new Vector3(1, 1, 1)));
	}

	[Fact]
	public void ATurnedBoxGetsTheBoxAroundIt()
	{
		var (lo, hi) = Picking.Transform(new Vector3(-1, 0, -1), new Vector3(1, 2, 1), Matrix4x4.CreateRotationY(MathF.PI / 4) * Matrix4x4.CreateTranslation(10, 0, 0));
		Assert.Equal(10 - MathF.Sqrt(2), lo.X, 3);
		Assert.Equal(10 + MathF.Sqrt(2), hi.X, 3);
		Assert.Equal(2, hi.Y, 3);
	}

	[Fact]
	public void TheGroundIsHitWhereTheRayMeetsIt()
	{
		// A flat ground at 5 m, 65 × 65 points around 0.
		var s = new WorldScene
		{
			World = null!, Name = "test", W = 65, H = 65, Size = 1, Heights = Enumerable.Repeat(5f, 65 * 65).ToArray(), Biomes = new int[65 * 65],
			Things = new(), BiomeColor = new byte[65 * 65 * 4], Mask = new byte[65 * 65 * 4], OceanDepth = new float[65 * 65], Limit = new float[65 * 65],
		};
		Assert.Equal(5f, Picking.HeightAt(s, 3.3f, -7.1f), 4);
		float t = Picking.HitGround(s, new Vector3(0, 25, 0), new Vector3(0, -1, 0))!.Value;
		Assert.Equal(20f, t, 2);
		Assert.Null(Picking.HitGround(s, new Vector3(0, 25, 0), new Vector3(0, 1, 0)));
		// A ray that leaves the area above the ground hits nothing (it hit 1000 m down past the edge).
		Assert.Null(Picking.HitGround(s, new Vector3(0, 25, 0), Vector3.Normalize(new Vector3(1, -0.3f, 0))));
	}
}
