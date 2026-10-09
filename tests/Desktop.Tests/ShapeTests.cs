using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Shape tool and its formula language (the web editor's editor/shapes.js).
public class ShapeTests
{
	private static double Eval(string src, double x = 0, double z = 0)
	{
		var f = Formula.Compile(src, new[] { "x", "z" });
		var env = new Formula.Env();
		env.Vars["x"] = x;
		env.Vars["z"] = z;
		return f(env);
	}

	[Theory]
	[InlineData("1 + 2 * 3", 7)]
	[InlineData("(1 + 2) * 3", 9)]
	[InlineData("2 ^ 3 ^ 2", 512)]
	[InlineData("-2 ^ 2", -4)]
	[InlineData("7 % 4", 3)]
	[InlineData("x > 1 ? 10 : 20", 10)]
	[InlineData("2 && 3", 3)]
	[InlineData("0 || 4", 4)]
	[InlineData("!0", 1)]
	[InlineData("smooth(0.5)", 0.5)]
	[InlineData("clamp(5, 0, 2)", 2)]
	[InlineData("max(1, x, 3)", 3)]
	[InlineData("round(2.5)", 3)]
	[InlineData("1e2 + .5", 100.5)]
	public void FormulasWorkOutLikeTheWebEditors(string src, double want)
	{
		Assert.Equal(want, Eval(src, x: 2), 9);
	}

	[Theory]
	[InlineData("", "the formula is empty")]
	[InlineData("1 +", "the formula ends too early")]
	[InlineData("(1 + 2", "“)” expected at the end")]
	[InlineData("y + 1", "there is no “y” (you can use x, z, pi)")]
	[InlineData("foo(1)", "there is no function “foo”")]
	[InlineData("1 2", "“2” is out of place")]
	[InlineData("1 # 2", "I do not understand “# 2”")]
	[InlineData("n(1)", "n(x, z) takes two numbers")]
	[InlineData("sin()", "sin(…) takes one number")]
	[InlineData("pow(x)", "pow(…) takes 2 numbers")]
	[InlineData("clamp(x, 0)", "clamp(…) takes 3 numbers")]
	[InlineData("max()", "max(…) takes at least one number")]
	public void MistakesAreExplained(string src, string message)
	{
		var ex = Assert.Throws<FormatException>(() => Formula.Compile(src, new[] { "x", "z" }));
		Assert.Equal(message, ex.Message);
	}

	// A pasted formula nested thousands deep is refused (it ran out of stack: the app closed).
	[Fact]
	public void DeepOrHugeFormulasAreRefused()
	{
		var names = new[] { "x", "z" };
		Assert.Equal("the formula is nested too deeply", Assert.Throws<FormatException>(() => Formula.Compile(new string('(', 1000) + "1" + new string(')', 1000), names)).Message);
		Assert.Equal("the formula is nested too deeply", Assert.Throws<FormatException>(() => Formula.Compile(new string('-', 3000) + "1", names)).Message);
		Assert.Equal("the formula is too long", Assert.Throws<FormatException>(() => Formula.Compile(string.Join("+", Enumerable.Repeat("1", 3000)), names)).Message);
		Assert.Equal(40, Eval(new string('(', 50) + "40" + new string(')', 50)), 9);
	}

	[Fact]
	public void EveryPresetCompiles()
	{
		foreach (var (_, name, src) in ShapePanel.Presets)
		{
			Formula.Compile(src, new[] { "x", "z", "d", "r", "h" });
		}
	}

	[Fact]
	public void AMoundRisesInTheMiddleAndUndoesInOneStep()
	{
		var s = EditTests.Flat(2);
		var f = Formula.Compile(ShapePanel.Presets[0].Formula, new[] { "x", "z", "d", "r", "h" });
		var (touched, clamped, bad) = s.Shape(64, 64, f, 10, 5, "Shape: Mound");
		Assert.True(touched > 250);
		Assert.False(clamped);
		Assert.Equal(0, bad);
		float H(int x, int z) => s.Scene.Heights[z * s.Scene.W + x];
		Assert.Equal(35, H(64, 64), 3);
		Assert.Equal(30, H(74, 64), 3);
		Assert.True(H(69, 64) > 30 && H(69, 64) < 35);
		Assert.Equal("Shape: Mound", s.UndoLabel);
		s.Undo();
		Assert.Equal(30, H(64, 64), 3);
	}

	[Fact]
	public void ShapesStopAtTheGameLimit()
	{
		var s = EditTests.Flat(2);
		var (_, clamped, _) = s.Shape(64, 64, Formula.Compile("h", new[] { "h" }), 5, 20, "Shape");
		Assert.True(clamped);
		Assert.Equal(38, s.Scene.Heights[64 * s.Scene.W + 64], 3);
	}

	[Fact]
	public void FormulasThatGiveNoNumberAreCounted()
	{
		var s = EditTests.Flat(2);
		var (touched, _, bad) = s.Shape(64, 64, Formula.Compile("sqrt(x) / 0 - 1 / 0", new[] { "x" }), 3, 1, "Shape");
		Assert.Equal(0, touched);
		Assert.True(bad > 0);
	}

	[AvaloniaFact]
	public void ThePanelSwitchesPresetsAndFormulas()
	{
		var p = new ShapePanel();
		p.PresetBox.SelectedIndex = 1;
		Assert.Equal("h * max(0, 1 - d / r)", p.FormulaBox.Text);
		Assert.Equal("Shape: Cone", p.Label);
		// Editing the formula makes it a formula of its own.
		p.FormulaBox.Text = "h * 2";
		Assert.Equal(ShapePanel.Presets.Length, p.PresetBox.SelectedIndex);
		Assert.Equal("Shape: Formula", p.Label);
		Assert.NotNull(p.Function);
		p.FormulaBox.Text = "h * ";
		Assert.Null(p.Function);
		Assert.Equal("Formula: the formula ends too early.", p.ErrorText.Text);
	}

	[AvaloniaFact]
	public void ClickingTheGroundPutsTheShapeThere()
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.KeyPress(Key.G, RawInputModifiers.None, PhysicalKey.G, "g");
		Assert.Equal(ToolMode.Shape, w.View.Mode);
		Assert.True(w.ShapePanel.Card.IsVisible);
		w.View.SetCamera(new Vector3(0, 100, 1), Vector3.Zero, 1);
		var q = Vector4.Transform(new Vector4(0, 30, 0, 1), w.View.ViewProj);
		var at = new Avalonia.Point((q.X / q.W + 1) / 2 * 1000, (1 - q.Y / q.W) / 2 * 1000);
		w.MouseDown(at, MouseButton.Left);
		w.MouseUp(at, MouseButton.Left);
		Assert.Equal(35, s.Scene.Heights[64 * s.Scene.W + 64], 1);
		Assert.StartsWith("Placed the shape", w.MessageText.Text);
		Assert.True(w.SaveButton.IsEnabled);
	}
}
