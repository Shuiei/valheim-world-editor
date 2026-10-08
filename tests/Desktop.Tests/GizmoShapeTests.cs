using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Select tool's handles as drawn: the ring lies flat at its radius, each arrow points along its
// own axis from just off the middle to its tip, the colours tell them apart (the hot one highlighted),
// and rays that cannot meet a handle give no answer.
public class GizmoShapeTests
{
	private static List<Vector3> Corners(float[] mesh)
	{
		Assert.Equal(0, mesh.Length % 9);
		return Enumerable.Range(0, mesh.Length / 3).Select(i => new Vector3(mesh[i * 3], mesh[i * 3 + 1], mesh[i * 3 + 2])).ToList();
	}

	[Fact]
	public void TheRingLiesFlatAroundTheMiddle()
	{
		var c = Corners(Gizmo.Mesh(Gizmo.Handle.Ring));
		Assert.True(c.Count > 100);
		foreach (var p in c)
		{
			float r = MathF.Sqrt(p.X * p.X + p.Z * p.Z);
			Assert.InRange(r, Gizmo.RingRadius - 0.1f, Gizmo.RingRadius + 0.1f);
			Assert.InRange(p.Y, -0.1f, 0.1f);
		}
		// All the way round.
		Assert.Contains(c, p => p.X > Gizmo.RingRadius - 0.1f);
		Assert.Contains(c, p => p.X < -Gizmo.RingRadius + 0.1f);
		Assert.Contains(c, p => p.Z > Gizmo.RingRadius - 0.1f);
		Assert.Contains(c, p => p.Z < -Gizmo.RingRadius + 0.1f);
	}

	[Theory]
	[InlineData(Gizmo.Handle.X)]
	[InlineData(Gizmo.Handle.Y)]
	[InlineData(Gizmo.Handle.Z)]
	public void EachArrowPointsAlongItsAxis(Gizmo.Handle h)
	{
		var axis = Gizmo.Axis(h);
		var c = Corners(Gizmo.Mesh(h));
		var along = c.Select(p => Vector3.Dot(p, axis)).ToList();
		// From just off the middle (the object stays grabbable) to the tip.
		Assert.InRange(along.Min(), 0.25f, 0.35f);
		Assert.InRange(along.Max(), 1.2f, 1.25f);
		// Thin: nothing far from the axis.
		Assert.All(c, p => Assert.True((p - Vector3.Dot(p, axis) * axis).Length() < 0.1f));
		// The head is wider than the shaft.
		float Width(float from, float to) => c.Where(p => Vector3.Dot(p, axis) >= from && Vector3.Dot(p, axis) <= to).Max(p => (p - Vector3.Dot(p, axis) * axis).Length());
		Assert.True(Width(0.97f, 0.99f) > Width(0.3f, 0.9f) * 2);
	}

	[Fact]
	public void TheHandlesHaveTheirOwnColoursAndTheHotOneIsHighlighted()
	{
		var colours = Enum.GetValues<Gizmo.Handle>().Select(h => Gizmo.Color(h, false)).ToList();
		Assert.Equal(colours.Count, colours.Distinct().Count());
		Assert.Equal(new Vector4(1, 0.3f, 0.37f, 1), Gizmo.Color(Gizmo.Handle.X, false));
		Assert.All(Enum.GetValues<Gizmo.Handle>(), h => Assert.Equal(new Vector4(1, 0.88f, 0.29f, 1), Gizmo.Color(h, true)));
	}

	[Fact]
	public void RaysThatCannotMeetGiveNoAnswer()
	{
		// Along an arrow seen end on: no point along it.
		Assert.Null(Gizmo.Along(new Vector3(0, 0, 5), -Vector3.UnitX, Vector3.Zero, Vector3.UnitX));
		// The ring's plane seen edge on, or behind the eye.
		Assert.Null(Gizmo.Heading(new Vector3(0, 5, 0), Vector3.UnitX, Vector3.Zero));
		Assert.Null(Gizmo.Heading(new Vector3(0, 5, 0), Vector3.UnitY, Vector3.Zero));
		// Looking down from above, east of the middle: heading 90.
		Assert.Equal(90, Gizmo.Heading(new Vector3(3, 5, 0), -Vector3.UnitY, Vector3.Zero)!.Value, 3);
	}
}
