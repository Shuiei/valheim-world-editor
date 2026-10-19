using System.Numerics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// --driver's commands on controls of a dialog of the window's own (number boxes, text boxes, lists, a switch), with no
/// world: what each sets, and what it says when there is nothing to set; then the 3D commands on a made-up area (the
/// camera at a height, the mouse in the air, a point out of view) and the commands that need none.
/// </summary>
public class DriverControlsTests
{
    private static readonly string[] Expected1 = ["Red"];
    /// <summary>
    /// A shown main window that tells and asks nothing.
    /// </summary>
    private static MainWindow Window()
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 800 };
        w.Show();
        w.Tell = _ => Task.CompletedTask;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        return w;
    }

    /// <summary>
    /// A row: a label, then these controls.
    /// </summary>
    private static StackPanel Row(string label, params Control[] controls)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new TextBlock { Text = label });
        row.Children.AddRange(controls);
        return row;
    }

    /// <summary>
    /// A dialog of the window holding these controls, shown and laid out.
    /// </summary>
    private static Window Dialog(MainWindow owner, params Control[] controls)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.AddRange(controls);
        var d = new Window { Width = 500, Height = 700, Content = panel };
        d.Show(owner);
        Dispatcher.UIThread.RunJobs();
        d.UpdateLayout();
        return d;
    }

    /// <summary>
    /// The command's answer, as JSON.
    /// </summary>
    private static async Task<JsonElement> Send(MainWindow w, string cmd)
    {
        string answer = await Driver.Run(w, cmd);
        Dispatcher.UIThread.RunJobs();
        return JsonDocument.Parse(answer).RootElement.Clone();
    }

    [AvaloniaFact]
    public async Task TheControlsAreSetByTheirLabelsAndWords()
    {
        var w = Window();
        try
        {
            NumericUpDown low = new() { Value = 5 }, high = new() { Value = 6 };
            TextBox from = new(), to = new();
            var colours = new ComboBox { ItemsSource = new[] { "Red", "Green" } };
            var flip = new ToggleButton { Content = "Flip me" };
            var letters = new ComboBox { Items = { new ComboBoxItem(), new ComboBoxItem { Content = "Zeta" } } };
            var d = Dialog(w, Row("Alpha", low, high), Row("Beta", from, to), Row("Gamma", colours), flip, letters);
            // Several number boxes beside a label: in order, an empty value for none.
            await Send(w, "set Alpha|1.5,");
            Assert.Equal(1.5m, low.Value);
            Assert.Null(high.Value);
            await Send(w, "set Beta|north, south");
            Assert.Equal(("north", "south"), (from.Text, to.Text));
            await Send(w, "set Gamma|green");
            Assert.Equal(1, colours.SelectedIndex);
            // A switch is switched as it is clicked.
            await Send(w, "click Flip me");
            Assert.True(flip.IsChecked);
            // A list whose entries are items, one of them empty.
            await Send(w, "choose zeta");
            Assert.Equal(1, letters.SelectedIndex);
            d.Close();
        }
        finally
        {
            w.CloseWithoutAsking();
        }
    }

    [AvaloniaFact]
    public async Task ALabelWithNothingToSetBesideItSaysSo()
    {
        var w = Window();
        try
        {
            var d = Dialog(w, Row("Delta", new Button { Content = "Go" }), Row("Epsilon", new ComboBox { ItemsSource = Expected1 }));
            Assert.Equal("nothing to set beside \"Delta\"", (await Assert.ThrowsAsync<InvalidOperationException>(() => Send(w, "set Delta|1"))).Message);
            // A list without the value asked for: nothing set.
            Assert.Equal("nothing to set beside \"Epsilon\"", (await Assert.ThrowsAsync<InvalidOperationException>(() => Send(w, "set Epsilon|purple"))).Message);
            d.Close();
        }
        finally
        {
            w.CloseWithoutAsking();
        }
    }

    // type, set and check without the '|' between their two parts give their usage, not a slicing error.
    [AvaloniaFact]
    public async Task ACommandWithoutItsBarGivesItsUsage()
    {
        var w = Window();
        try
        {
            Assert.Equal("usage: type <hint>|<text>", (await Assert.ThrowsAsync<InvalidOperationException>(() => Send(w, "type Name"))).Message);
            Assert.Equal("usage: set <label>|<value>", (await Assert.ThrowsAsync<InvalidOperationException>(() => Send(w, "set Delta"))).Message);
            Assert.Equal("usage: check <label>|on|off", (await Assert.ThrowsAsync<InvalidOperationException>(() => Send(w, "check"))).Message);
        }
        finally
        {
            w.CloseWithoutAsking();
        }
    }

    [AvaloniaFact]
    public async Task TheCommandsThatNeedNoWorld()
    {
        var w = Window();
        try
        {
            Assert.Equal("ok", (await Send(w, "spirv")).GetProperty("spirvCross").GetString());
            // Every modifier's name, and one that is none.
            Assert.Equal("start", (await Send(w, "key F24 shift ctrl alt meta")).GetProperty("page").GetString());
            // No area: the camera is left alone, the 3D commands say so.
            await Send(w, "camera 0 0 30 50 70 12");
            Assert.Equal("no area open", (await Assert.ThrowsAsync<InvalidOperationException>(() => Send(w, "mouse move 0 0"))).Message);
            Assert.Equal("no area open", (await Assert.ThrowsAsync<InvalidOperationException>(() => Send(w, "mouse3 move 0 5 0"))).Message);
        }
        finally
        {
            w.CloseWithoutAsking();
        }
    }

    [AvaloniaFact]
    public async Task TheThreeDCommandsOnAMadeUpArea()
    {
        var w = Window();
        try
        {
            var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(40, 30, 40), Vector3.Zero, 1, false));
            Assert.Equal("no area", w.View.CameraSays());
            w.View.Show(s.Scene, null);
            w.Edit(s);
            // The camera on a spot at a height (inside a dungeon), then without one.
            var state = await Send(w, "camera 40 40 30 50 70 12");
            Assert.Equal(70, state.GetProperty("distance").GetSingle(), 0.01f);
            // Reset: the camera the editor starts with, back from walking (the area's middle still its target).
            w.View.CycleEye();
            state = await Send(w, "camera reset");
            Assert.Equal(GlView.EyeMode.Orbit, w.View.Eye);
            Assert.Equal(140, state.GetProperty("distance").GetSingle(), 0.01f);
            Assert.Equal(0.8f, state.GetProperty("yaw").GetSingle(), 0.001f);
            await Send(w, "camera 40 40 30 50 60");
            // Headless windows draw no frame: the camera's matrix the mouse commands use is set as a frame would.
            var c = new Vector3(40 - s.Scene.Cx, 30, -(40 - s.Scene.Cz));
            w.View.SetCamera(c + new Vector3(0, 60, 1), c, 1.5f);
            await Send(w, "mouse3 down 40 5 40 right shift");
            await Send(w, "mouse3 up 40 5 40 right");
            // 2 m up, looking up into the sky: the ground and a point a metre over it are behind the camera.
            w.View.SetCamera(c + new Vector3(0, 2, 0), c + new Vector3(0, 12, 0.1f), 1.5f);
            // The error says where the camera is (world metres): the orbit's, and the eye the mouse was read with.
            const string Camera = "yaw 30°, pitch 50°, distance 60 m; eye at the last frame 40 32 40; no frame drawn yet)";
            string flat = (await Assert.ThrowsAsync<InvalidOperationException>(() => Send(w, "mouse move 40 40"))).Message;
            Assert.StartsWith("40, 40 is not in view (camera: Orbit, target 40 ", flat);
            Assert.EndsWith(Camera, flat);
            string air = (await Assert.ThrowsAsync<InvalidOperationException>(() => Send(w, "mouse3 move 40 1 40"))).Message;
            Assert.StartsWith("not in view (camera: Orbit, target 40 ", air);
            Assert.EndsWith(Camera, air);
        }
        finally
        {
            w.CloseWithoutAsking();
        }
    }
}
