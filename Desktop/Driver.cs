using System.Text.Json;
using Avalonia.Threading;

namespace TerrainEditor.Desktop;

/// <summary>
/// --driver: the editor driven by another program (the visual tests), one command per line on its
/// input, one answer per line on its output ("@@ ok &lt;json&gt;" or "@@ error &lt;message&gt;"), so a single
/// editor serves many tests. Commands:
/// </summary>
/// <remarks>
/// <code>
///    gamedata &lt;folder&gt;              the game's data from the tests' copies (tests/fixtures): where
///                                   there is no Valheim (CI), so that worlds open
///    world &lt;folder&gt;                 open a world (its map)
///    area &lt;zx&gt; &lt;zz&gt; &lt;size&gt;          open an area in the 3D editor
///    map                            back to the map
///    stroke &lt;tool&gt; &lt;x&gt; &lt;z&gt; &lt;steps&gt;  a brush stroke at world x, z (tool: raise, lower, smooth...)
///    picture &lt;file.png&gt;             a picture of the view shown (3D or map), once it is drawn
///    shot &lt;file.png&gt;                a picture of the whole window, panels and view (documentation)
///    look &lt;game|seethrough&gt; &lt;on|off&gt;, look res &lt;sharp|balanced|fast&gt;
///                                   the View panel's Look switches
///    camera &lt;x&gt; &lt;z&gt; &lt;yaw°&gt; &lt;pitch°&gt; &lt;distance&gt; [y]  the 3D view's camera on world x, z (at height y:
///                                               inside a dungeon)
///    click &lt;text&gt;                   the visible button, switch or box labelled so, or whose words
///                                   begin so (windows and dialogs)
///    choose &lt;text&gt;                  the entry so named in whichever visible list has it
///    type &lt;hint&gt;|&lt;text&gt;             the visible text box whose placeholder contains hint gets text
///    set &lt;label&gt;|&lt;value&gt;            the slider, text box(es: a,b) or list beside the label so worded
///    panel &lt;view|history|help|none&gt; the right-hand panel shown
///    message [text]                 the status bar's message (none: cleared)
///    wait &lt;ms&gt;                      a pause (a brush stroke works frame by frame while held)
///    bench &lt;seconds&gt;                the 3D camera turns on its own; the frame rates come back
///    mouse &lt;down|move|up&gt; &lt;x&gt; &lt;z&gt; [left|right|middle] [shift|ctrl|alt ...]
///                                   the mouse at world x, z over the 3D view (real pointer events)
///    wheel &lt;x&gt; &lt;z&gt; &lt;steps&gt;          the wheel there (positive: zoom in)
///    key &lt;name&gt; [shift|ctrl|alt ...] a key pressed in the window (Avalonia key names: E, Escape, Enter, D1...)
///    mapview &lt;x&gt; &lt;z&gt; &lt;m per pixel&gt;  the map looking there
///    mappick &lt;zone x&gt; &lt;zone z&gt;      the map's zone picked (as a click on it)
///    search &lt;text&gt;                  the map's search (objects by kind)
///    hit &lt;n&gt;                        the search's result n (from 0) picked, as a click in its list
///    zones                          the map's zone filter: show the matching zones
///    workshop [file.blueprint]      the Workshop opened (on that blueprint); how long it took
///    state                          what is shown, the objects, what is pending, frames drawn
///    quit
/// </code>
/// Also spirv (SPIRV-Cross loads), mouse3 (a point in the air), inspect &lt;prefab&gt; and check &lt;label&gt;|on|off.
/// </remarks>
public static class Driver
{
    /// <summary>
    /// How long a picture may wait for the view to be ready: the world map's first build on a slow computer (CI's
    /// two cores, slower still while its coverage is measured) takes more than a minute.
    /// </summary>
    private static readonly TimeSpan PictureWait = TimeSpan.FromMinutes(3);

    /// <summary>The View panel's 3D resolution choices, in their list's order.</summary>
    private static readonly string[] Resolutions = ["sharp", "balanced", "fast"];

    /// <summary>
    /// Reads commands on a thread of its own, runs each on the window's thread and writes its answer; "@@ ready" once
    /// listening. The window closes when the input ends or after quit.
    /// </summary>
    public static void Start(MainWindow w)
    {
        var thread = new Thread(() =>
        {
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                string cmd = line.Trim();
                if (cmd.Length == 0)
                {
                    continue;
                }
                string answer;
                try
                {
                    answer = "@@ ok " + Dispatcher.UIThread.InvokeAsync(() => Run(w, cmd)).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    answer = "@@ error " + (ex.InnerException ?? ex).Message.Replace('\n', ' ');
                }
                Console.WriteLine(answer);
                Console.Out.Flush();
                if (cmd == "quit")
                {
                    break;
                }
            }
            Dispatcher.UIThread.Post(w.CloseWithoutAsking);
        })
        {
            IsBackground = true,
            Name = "Driver"
        };
        thread.Start();
        Console.WriteLine("@@ ready");
        Console.Out.Flush();
    }

    /// <summary>Runs one command; its answer's JSON (usually <see cref="State"/>). Throws for an unknown or failed command.</summary>
    internal static async Task<string> Run(MainWindow w, string cmd)
    {
        string[] a = cmd.Split(' ', 2);
        string[] args = a.Length > 1 ? a[1].Split(' ') : [];
        float F(int i) => float.Parse(args[i], System.Globalization.CultureInfo.InvariantCulture);
        switch (a[0])
        {
            case "spirv":
                // SPIRV-Cross (the terrain shader from Valheim for Windows) loads in this build.
                TerrainEditor.App.TerrainShader.SelfTest();
                return "{\"spirvCross\":\"ok\"}";
            case "gamedata":
                TerrainEditor.App.GameData.UseCopies(a[1]);
                return "{}";
            case "world":
                await w.OpenWorld(() => Task.Run(() => WorldSession.Open(a[1])), "Opening the world…");
                return State(w);
            case "area":
                await w.EditArea((int)F(0), (int)F(1), (int)F(2));
                return State(w);
            case "map":
                w.ShowMap();
                return State(w);
            case "stroke":
            {
                var s = w.Session ?? throw new InvalidOperationException("no area open");
                var tool = Enum.Parse<BrushTool>(args[0], ignoreCase: true);
                float gx = F(1) - (s.Scene.X0 * 64 - 32), gz = F(2) - (s.Scene.Z0 * 64 - 32);
                s.BeginStroke(tool, gx, gz);
                for (int i = 0; i < (int)F(3); i++)
                {
                    s.StrokeStep(gx, gz, 0.05f);
                }
                s.EndStroke();
                return State(w);
            }
            case "mouse":
            {
                var p = ScreenAt(w, F(1), F(2));
                string button = args.Length > 3 ? args[3] : "left";
                Mouse(w, args[0], p, button, Mods(args.Skip(4)));
                return State(w);
            }
            case "mouse3":
            {
                // A point in the air: world x, height above the ground, z (pieces, for the Workshop).
                var sc = w.View.Scene ?? throw new InvalidOperationException("no area open");
                var g = new System.Numerics.Vector2(F(1) - (sc.X0 * 64 - 32), F(3) - (sc.Z0 * 64 - 32));
                var p = w.View.ScreenOfGrid(g, F(2)) ?? throw new InvalidOperationException("not in view");
                Mouse(w, args[0], p, args.Length > 4 ? args[4] : "left", Mods(args.Skip(5)));
                return State(w);
            }
            case "wheel":
            {
                var p = ScreenAt(w, F(0), F(1));
                var pointer = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true);
                w.Surface.RaiseEvent(new Avalonia.Input.PointerWheelEventArgs(w.Surface, pointer, w.Surface, p, 0, new Avalonia.Input.PointerPointProperties(), Avalonia.Input.KeyModifiers.None, new Avalonia.Vector(0, F(2))));
                return State(w);
            }
            case "key":
            {
                var key = Enum.Parse<Avalonia.Input.Key>(args[0], ignoreCase: true);
                w.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = key, KeyModifiers = Mods(args.Skip(1)), Source = w });
                w.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyUpEvent, Key = key, KeyModifiers = Mods(args.Skip(1)), Source = w });
                return State(w);
            }
            case "mapview":
                w.MapPage!.Map.LookAt(F(0), F(1), F(2));
                return State(w);
            case "mappick":
                w.MapPage!.Pick((int)F(0), (int)F(1));
                return State(w);
            case "search":
                w.MapPage!.SearchBox.Text = a[1];
                await w.MapPage.Search();
                return State(w);
            case "hit":
                w.MapPage!.Hits.SelectedIndex = int.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture);
                return State(w);
            case "zones":
                w.MapPage!.NoBuildBox.IsChecked = w.MapPage.NoEditBox.IsChecked = w.MapPage.OnlyGenBox.IsChecked = false;
                await w.MapPage.ShowMatching();
                return State(w);
            case "picture":
                await (w.MapShown ? w.MapPage!.Map.Picture(a[1]) : w.View.Picture(a[1])).WaitAsync(PictureWait);
                return State(w);
            case "shot":
            {
                // Once the view shown has its area and models in (its own picture waits for that).
                string glPicture = Path.Combine(Path.GetTempPath(), $"vwe-shot-{Guid.NewGuid():N}.png");
                try
                {
                    if (w.MapShown || w.Session != null)
                    {
                        await (w.MapShown ? w.MapPage!.Map.Picture(glPicture) : w.View.Picture(glPicture)).WaitAsync(PictureWait);
                    }
                    await WindowShot.Save(w, a[1]);
                }
                finally
                {
                    File.Delete(glPicture);
                }
                return State(w);
            }
            case "look":
                switch (args[0])
                {
                    case "game": w.GameLookBox.IsChecked = args[1] == "on"; break;
                    case "seethrough": w.SeeThroughBox.IsChecked = args[1] == "on"; break;
                    case "res": w.ResolutionBox.SelectedIndex = Array.IndexOf(Resolutions, args[1]); break;
                    default: throw new InvalidOperationException("unknown look " + args[0]);
                }
                return State(w);
            case "type":
            {
                int bar = a[1].IndexOf('|');
                string hint = a[1][..bar], text = a[1][(bar + 1)..];
                var box = Find<Avalonia.Controls.TextBox>(w, b => (b.PlaceholderText ?? "").Contains(hint, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"no text box \"{hint}\"");
                box.Text = text;
                return State(w);
            }
            case "set":
            {
                int bar = a[1].IndexOf('|');
                string label = a[1][..bar], value = a[1][(bar + 1)..];
                var tb = Find<Avalonia.Controls.TextBlock>(w, t => string.Equals(t.Text?.Trim(), label, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"no label \"{label}\"");
                // The row holding the label: the first control after it.
                var row = Avalonia.VisualTree.VisualExtensions.GetVisualParent(tb);
                for (int up = 0; up < 3 && row != null; up++, row = Avalonia.VisualTree.VisualExtensions.GetVisualParent(row))
                {
                    var c = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row).OfType<Avalonia.Controls.Control>()
                        .FirstOrDefault(c => c is Avalonia.Controls.Slider or Avalonia.Controls.TextBox or Avalonia.Controls.ComboBox or Avalonia.Controls.NumericUpDown && c.IsEffectivelyVisible);
                    switch (c)
                    {
                        case Avalonia.Controls.Slider sl:
                            sl.Value = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                            return State(w);
                        case Avalonia.Controls.NumericUpDown:
                        {
                            // Several boxes in the row (Height min, max): in order, comma separated; empty for none.
                            var boxes = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row).OfType<Avalonia.Controls.NumericUpDown>().Where(b => b.IsEffectivelyVisible).ToList();
                            string[] values = value.Split(',');
                            for (int k = 0; k < Math.Min(values.Length, boxes.Count); k++)
                            {
                                boxes[k].Value = values[k].Trim().Length == 0 ? null : decimal.Parse(values[k], System.Globalization.CultureInfo.InvariantCulture);
                            }
                            return State(w);
                        }
                        case Avalonia.Controls.TextBox:
                        {
                            // Several boxes in the row (Size % from, to): the values in order, comma separated.
                            var boxes = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row).OfType<Avalonia.Controls.TextBox>().Where(b => b.IsEffectivelyVisible).ToList();
                            string[] values = value.Split(',');
                            for (int k = 0; k < Math.Min(values.Length, boxes.Count); k++)
                            {
                                boxes[k].Text = values[k].Trim();
                            }
                            return State(w);
                        }
                        case Avalonia.Controls.ComboBox cb when IndexOf(cb, value) >= 0:
                            cb.SelectedIndex = IndexOf(cb, value);
                            return State(w);
                    }
                }
                throw new InvalidOperationException($"nothing to set beside \"{label}\"");
            }
            case "panel":
                w.ShowRightPanel(args[0]);
                return State(w);
            case "message":
                w.StatusMessage = a.Length > 1 ? a[1] : "";
                return State(w);
            case "wait":
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(F(0), 0, 60000)));
                return State(w);
            case "inspect":
            {
                // inspect <prefab>: the first such object selected, its inspector open.
                var scene = w.View.Scene ?? throw new InvalidOperationException("no area open");
                // One holding something first (a chest with contents).
                var all = Enumerable.Range(0, scene.Things.Count).Where(k => !scene.Things[k].Gone && scene.Things[k].Prefab == Save.StableHash.Of(a[1])).ToList();
                int i = all.Cast<int?>().FirstOrDefault(k => scene.Session is { } ss && TerrainEditor.App.ObjectData.Bytes(scene.World, ss.Edits, scene.Things[k!.Value].Id) is { } b
                    && Save.ZdoData.Parse(b).GetBytes(TerrainEditor.App.ObjectData.ItemsKey) != null) ?? (all.Count > 0 ? all[0] : -1);
                if (i < 0)
                {
                    throw new InvalidOperationException($"no {a[1]} here");
                }
                w.Tools.ChooseMode(ToolMode.Select);
                w.View.Select([i]);
                w.View.Focus(scene.Things[i].Position);
                w.Inspect();
                return State(w);
            }
            case "camera":
                w.View.Orbit(F(0), F(1), F(2), F(3), F(4), args.Length > 5 ? F(5) : null);
                return State(w);
            case "click":
            {
                var c = Find<Avalonia.Controls.Button>(w, b => string.Equals(TextOf(b), a[1], StringComparison.OrdinalIgnoreCase))
                    ?? Find<Avalonia.Controls.Button>(w, b => TextOf(b).StartsWith(a[1] + " ", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"no button \"{a[1]}\"");
                if (c is Avalonia.Controls.Primitives.ToggleButton t)
                {
                    t.IsChecked = t.IsChecked != true;
                }
                c.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                return State(w);
            }
            case "check":
            {
                // check <label>|on|off: a check box or switch by its text.
                int bar = a[1].IndexOf('|');
                string label = a[1][..bar];
                var box = Find<Avalonia.Controls.CheckBox>(w, b => string.Equals(TextOf(b).Trim(), label, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"no check box \"{label}\"");
                box.IsChecked = a[1][(bar + 1)..] == "on";
                return State(w);
            }
            case "choose":
            {
                var box = Find<Avalonia.Controls.ComboBox>(w, b => IndexOf(b, a[1]) >= 0) ?? throw new InvalidOperationException($"no list with \"{a[1]}\"");
                box.SelectedIndex = IndexOf(box, a[1]);
                return State(w);
            }
            case "bench":
            {
                string result = await w.View.Benchmark(F(0)).WaitAsync(TimeSpan.FromSeconds(F(0) + 60));
                return JsonSerializer.Serialize(new { bench = result });
            }
            case "workshop":
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await w.OpenWorkshop(a.Length > 1 ? a[1] : null);
                return JsonSerializer.Serialize(new { opened = sw.ElapsedMilliseconds, objects = w.View.Scene?.Things.Count(t => !t.Gone) ?? 0 });
            }
            case "state":
            case "quit":
                return State(w);
            default:
                throw new InvalidOperationException("unknown command " + a[0]);
        }
    }

    /// <summary>Key modifiers from their names (shift, ctrl, alt; others ignored).</summary>
    private static Avalonia.Input.KeyModifiers Mods(IEnumerable<string> names)
    {
        var m = Avalonia.Input.KeyModifiers.None;
        foreach (string n in names)
        {
            m |= n switch { "shift" => Avalonia.Input.KeyModifiers.Shift, "ctrl" => Avalonia.Input.KeyModifiers.Control, "alt" => Avalonia.Input.KeyModifiers.Alt, _ => Avalonia.Input.KeyModifiers.None };
        }
        return m;
    }

    /// <summary>The first visible, enabled control of that type in the window or its open dialogs (the newest first).</summary>
    private static T? Find<T>(MainWindow w, Func<T, bool> match) where T : Avalonia.Controls.Control
    {
        IEnumerable<Avalonia.Controls.Window> windows = new Avalonia.Controls.Window[] { w }.Concat(w.OwnedWindows);
        foreach (var win in windows.Reverse())
        {
            foreach (var c in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(win).OfType<T>())
            {
                if (c.IsEffectivelyVisible && c.IsEffectivelyEnabled && match(c))
                {
                    return c;
                }
            }
        }
        return null;
    }

    /// <summary>A control's own words: its text content, or every text inside it.</summary>
    private static string TextOf(Avalonia.Controls.ContentControl c) => c.Content is string s ? s.Trim()
        : string.Join(" ", Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c).OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text?.Trim()).Where(t => !string.IsNullOrEmpty(t))).Trim();

    /// <summary>The index of a list's entry by its text (any case), -1 when none.</summary>
    private static int IndexOf(Avalonia.Controls.ComboBox box, string text)
    {
        int i = 0;
        foreach (object? item in box.Items)
        {
            string? name = item is Avalonia.Controls.ContentControl cc ? cc.Content?.ToString() : item?.ToString();
            if (string.Equals(name?.Trim(), text, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
            i++;
        }
        return -1;
    }

    /// <summary>Where world x, z (on the ground) is over the 3D view; throws when it is not in view.</summary>
    private static Avalonia.Point ScreenAt(MainWindow w, float x, float z)
    {
        var s = w.View.Scene ?? throw new InvalidOperationException("no area open");
        var g = new System.Numerics.Vector2(x - (s.X0 * 64 - 32), z - (s.Z0 * 64 - 32));
        return w.View.ScreenOfGrid(g, 0) ?? throw new InvalidOperationException($"{x}, {z} is not in view");
    }

    /// <summary>The one mouse pointer the driver's events come from.</summary>
    private static readonly Avalonia.Input.Pointer TheMouse = new(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true);

    /// <summary>A real pointer event over the 3D view: down, move (with the button held) or up.</summary>
    private static void Mouse(MainWindow w, string what, Avalonia.Point p, string button, Avalonia.Input.KeyModifiers mods)
    {
        var raw = (Avalonia.Input.RawInputModifiers)mods;
        var (kind, down) = button switch
        {
            "right" => (what == "up" ? Avalonia.Input.PointerUpdateKind.RightButtonReleased : Avalonia.Input.PointerUpdateKind.RightButtonPressed, Avalonia.Input.RawInputModifiers.RightMouseButton),
            "middle" => (what == "up" ? Avalonia.Input.PointerUpdateKind.MiddleButtonReleased : Avalonia.Input.PointerUpdateKind.MiddleButtonPressed, Avalonia.Input.RawInputModifiers.MiddleMouseButton),
            _ => (what == "up" ? Avalonia.Input.PointerUpdateKind.LeftButtonReleased : Avalonia.Input.PointerUpdateKind.LeftButtonPressed, Avalonia.Input.RawInputModifiers.LeftMouseButton),
        };
        var surface = w.Surface;
        switch (what)
        {
            case "down":
                _held = down;
                surface.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(surface, TheMouse, surface, p, 0, new Avalonia.Input.PointerPointProperties(raw | down, kind), mods, 1));
                break;
            case "move":
                surface.RaiseEvent(new Avalonia.Input.PointerEventArgs(Avalonia.Input.InputElement.PointerMovedEvent, surface, TheMouse, surface, p, 0, new Avalonia.Input.PointerPointProperties(raw | _held, Avalonia.Input.PointerUpdateKind.Other), mods));
                break;
            default:
                _held = Avalonia.Input.RawInputModifiers.None;
                surface.RaiseEvent(new Avalonia.Input.PointerReleasedEventArgs(surface, TheMouse, surface, p, 0, new Avalonia.Input.PointerPointProperties(raw, kind), mods, kind == Avalonia.Input.PointerUpdateKind.RightButtonReleased ? Avalonia.Input.MouseButton.Right : kind == Avalonia.Input.PointerUpdateKind.MiddleButtonReleased ? Avalonia.Input.MouseButton.Middle : Avalonia.Input.MouseButton.Left));
                break;
        }
    }

    /// <summary>The mouse button held since the last down, for the moves.</summary>
    private static Avalonia.Input.RawInputModifiers _held;

    /// <summary>What is shown, as JSON: page, objects, pending, frames drawn, the camera, the map, models and more.</summary>
    private static string State(MainWindow w)
    {
        var s = w.Session;
        return JsonSerializer.Serialize(new
        {
            page = w.MapShown ? "map" : s != null ? "editor" : "start",
            objects = s?.Scene.Things.Count(t => !t.Gone) ?? 0,
            pending = w.World?.Pending.Zones ?? 0,
            frames = w.MapShown ? w.MapPage!.Map.FramesDrawn : w.View.FramesDrawn,
            glErrors = w.View.GlErrors + (w.MapPage?.Map.GlErrors ?? 0),
            selected = w.View.Selected.Count,
            mode = w.Tools.Mode.ToString(),
            message = w.MessageText.Text ?? "",
            added = w.World?.Pending.Added ?? 0,
            yaw = w.View.Camera.Yaw,
            distance = w.View.Camera.Distance,
            targetX = w.View.Camera.Target.X,
            mapX = w.MapPage?.Map.Center.X ?? 0,
            mapScale = w.MapPage?.Map.MetersPerPixel ?? 0,
            pins = w.MapPage?.Map.Pins.Count ?? 0,
            matches = w.MapPage?.Matches.Count ?? 0,
            detail = w.MapPage?.Map.DetailShown ?? false,
            models = w.View.Models != null,
            // The game's look copied from Valheim and in use (else plain colours: no Valheim, as in CI).
            gameLook = w.View.GameLookLoaded,
            modelParts = w.View.LastStats?.Instances ?? 0,
            modelsLoading = w.View.LastStats?.PendingModels ?? 0,
        });
    }
}
