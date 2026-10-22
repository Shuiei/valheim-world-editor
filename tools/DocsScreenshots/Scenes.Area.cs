using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The Area tool's pictures added with the documentation's rewrite (area.md and its guides): cut and fill, a paste
/// that clears its site, zones marked for reset, and the before and after pictures they share.
/// </summary>
public sealed partial class Scenes
{
    /// <summary>
    /// area-cut-fill.jpg: a box over a hillside with Flatten chosen, No limit off: the panel's cut and fill line says
    /// how much ground is out of reach of the ±8 m limit, and the line under it compares with the generated ground.
    /// </summary>
    private void AreaCutFill(Editor e)
    {
        Bare(e, unsaved: false);
        // A hill on the bare zone's west half, raised within the limit.
        foreach (var p in new[] { P(-12, 0), P(-16, 6), P(-16, -6) })
        {
            Stroke(e, "D1", p, 24, hold: 3000, steps: 6);
        }
        AreaAction(e, P(-24, -14), P(10, 14), "Flatten", apply: false);
        e.Send("set Height|46");
        Camera(e, 4, 0, 200, 35, 70);
        e.Send("wait 500");
        e.Shot(Out("area-cut-fill.jpg"));
    }

    /// <summary>
    /// area-reset.jpg: a box over two zones away from any building, Reset zones chosen with both switches on, after
    /// Reset zones… : the two zones outlined red, the zone borders drawn.
    /// </summary>
    private void AreaReset(Editor e)
    {
        Bare(e, unsaved: false, size: 3);
        AreaAction(e, P(-20, -12), P(44, 12), "Reset zones", apply: false);
        e.Send("check Keep my buildings|on");
        e.Send("check Reset ground edits too|on");
        e.Send("key Return");
        e.Send("click Reset when saving");
        Camera(e, 45, 0, 200, 60, 150);
        e.Send("wait 500");
        e.Shot(Out("area-reset.jpg"), keepMessage: true);
    }

    /// <summary>
    /// paste-clear-site.jpg: a stone hut's copy pasted into the side of a grassy mound with Clear the site on: before
    /// the click (the see-through copy sunk into the mound) and after it (the ground dug away round the walls).
    /// </summary>
    private void PasteClearSite(Editor e)
    {
        Bare(e, unsaved: false);
        // The stone hut on the zone's east side (left on the picture), on levelled ground.
        Level(e, P(10, -10), P(30, 10));
        e.Send("key T");
        Pieces(e);
        Kinds(e, "stone_floor_2x2");
        e.Send("click Grid");
        e.Send("set Spacing|2");
        Drag(e, P(16, -3), P(24, 3));
        e.Send("key Return");
        Kinds(e, "stone_wall_2x1");
        e.Send("click Line");
        e.Send("click Rectangle");
        e.Send("set Layers|3");
        Drag(e, P(16, -3), P(24, 3));
        e.Send("key Return");
        e.Send("key Escape");
        e.Send("key B");
        e.Send("click Box");
        Drag(e, P(13, -6), P(27, 6));
        e.Send("choose Copy and paste");
        e.Send("key C ctrl");
        e.Send("click Clear (Esc)");
        // A gentle mound on the west side (right on the picture).
        foreach (var p in new[] { P(-16, 0), P(-18, 6), P(-18, -6) })
        {
            Stroke(e, "D1", p, 26, hold: 2000, steps: 4);
        }
        e.Send("key B");
        e.Send("key V ctrl");
        // Only the clearing shapes the ground: the copied ground (flat under the hut) is left out.
        e.Send("check Ground shape and paint|off");
        e.Send("check Clear the site (dig the ground in its way, take away trees and rocks)|on");
        // Lowered into the slope: the clearing digs it out.
        e.Send("set Height|-1.5");
        Camera(e, -6, 0, 200, 40, 40);
        e.Send("wait 300");
        var at = P(-8, 0);
        Move(e, P(-8, 1));
        e.Send("wait 300");
        Move(e, at);
        e.Send("wait 500");
        string before = Out("before-paste-clear-site.jpg"), after = Out("after-paste-clear-site.jpg");
        e.Shot(before);
        e.Send($"mouse down {at.X} {at.Z}");
        e.Send($"mouse up {at.X} {at.Z}");
        // Onto the sky, off the ground: no preview of the tool over what was just done.
        e.Point(1300, 150);
        e.Send("wait 500");
        e.Shot(after, keepMessage: true);
        Halves(Out("paste-clear-site.jpg"), ("Before the click", "After the click"), before, after, new HalvesCut(720));
    }

    /// <summary>
    /// area-backup.jpg: a hut griefed (part of its near wall gone) and a polygon round it with Restore from a backup
    /// chosen, the Backup list open on the game's backup, Your buildings on; then the same view after Restore the
    /// selection, the walls back. On a copy of the Docs world, with a backup of it made after the hut was saved.
    /// </summary>
    private void AreaBackup(Editor e)
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vwe-docs-backup-{Environment.ProcessId}");
        string world = System.IO.Path.Combine(root, "Docs");
        CopyFolder(World, world);
        try
        {
            var date = SavedHutWithBackup(e, root, world);
            GriefHut(e);
            RestoreFromBackupShot(e, date);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A hut built and saved on the world copy, then a copy of that world made as the game's own backup beside it.
    /// </summary>
    /// <returns>The backup's date, as its list entry shows it.</returns>
    private static DateTime SavedHutWithBackup(Editor e, string root, string world)
    {
        e.Send($"world {world}");
        e.Send($"area {ZoneX} {ZoneZ} 1");
        e.CloseLocationNote();
        foreach (string sw in new[] { "Water", "Limit marks", "Unsaved marks", "View" })
        {
            e.Send($"click {sw}");
        }
        Level(e);
        Hut(e, P(-4, -3), P(4, 3));
        e.Send("key Escape");
        e.Send("click Save to world");
        e.Send("click Save");
        e.Send("wait 1000");
        e.Send("click OK");
        // The game's own backup of the world with the hut, as it makes them when it saves.
        var date = new DateTime(2026, 10, 20, 18, 0, 0, DateTimeKind.Local);
        string backup = System.IO.Path.Combine(root, "Docs_backup_auto-20261020180000");
        CopyFolder(world, backup);
        Directory.SetLastWriteTime(backup, date);
        return date;
    }

    /// <summary>The grief: part of the hut's near (north) wall taken down, both rows.</summary>
    private static void GriefHut(Editor e)
    {
        e.Send("key E");
        Camera(e, 6, 0, 200, 24, 22);
        e.Send("wait 300");
        Click(e, P(2, 1.5));
        foreach (var p in new[] { P(0, 1.5), P(-2, 1.5), P(2, -2.5), P(0, -2.5), P(-2, -2.5) })
        {
            Click(e, p, "ctrl");
        }
        e.Send("key Delete");
    }

    /// <summary>
    /// A polygon round the hut with Restore from a backup and the backup chosen (before), then the selection restored
    /// (after), side by side.
    /// </summary>
    private void RestoreFromBackupShot(Editor e, DateTime date)
    {
        // The polygon drawn from above, where no wall is in the way of the points.
        Camera(e, 0, 0, 200, 80, 40);
        e.Send("wait 300");
        e.Send("key B");
        e.Send("click Polygon");
        foreach (var p in new[] { P(-7, -6), P(7, -6), P(7, 6), P(-7, 6) })
        {
            Click(e, p);
        }
        e.Send("key Return");
        e.Send("choose Restore from a backup");
        e.Send("set Soft edge|2");
        e.Send("click Your buildings");
        e.Send(string.Format(System.Globalization.CultureInfo.InvariantCulture, "choose Game · {0:g}", date));
        Camera(e, 8, 0, 200, 26, 28);
        e.Send("wait 500");
        string before = Out("before-area-backup.jpg"), after = Out("after-area-backup.jpg");
        ShotWithList(e, before, 300, 492);
        e.Send("key Return");
        WaitForMessage(e, "Restored");
        e.Shot(after, keepMessage: true);
        // The open list is the before picture's; the panel's backup line (read by the restore) and the status bar are
        // the after picture's.
        Halves(Out("area-backup.jpg"), ("Griefed", "After Restore the selection"), before, after,
            new HalvesCut(1000, AreaPanelRight, Keep: [MaskCard], KeepBefore: [new SKRectI(178, 508, 404, 580)]));
    }

    /// <summary>
    /// area-heightmap.jpg: the Heightmap action after Export the area and Import… of the picture just exported: the
    /// panel's picture line, Lowest and Highest, Put it into the ground and Cancel, the area seen from above.
    /// </summary>
    /// <remarks>
    /// Import… opens the system's file picker. With a desktop session bus it would be the desktop's own (a portal, on
    /// the real screen, out of the display's reach), so the scene runs only when DBUS_SESSION_BUS_ADDRESS names a bus
    /// that is not there (unix:path=/nonexistent): the picker is then GTK's, on the display, and the picture's path is
    /// typed into it.
    /// </remarks>
    private void AreaHeightmap(Editor e)
    {
        string bus = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS") ?? "";
        if (!bus.StartsWith("unix:path=", StringComparison.Ordinal) || File.Exists(bus["unix:path=".Length..]))
        {
            Console.WriteLine("area-heightmap: skipped (DBUS_SESSION_BUS_ADDRESS must be unix:path=<no such file>, or the file picker opens on the desktop)");
            return;
        }
        Bare(e, unsaved: false);
        Stroke(e, "D1", P(-8, 4), 20, hold: 2500, steps: 5);
        e.Send("key B");
        e.Send("choose Heightmap");
        string said = e.Send("click Export the area").GetProperty("message").GetString() ?? "";
        const string Written = "Heightmap written to ";
        int at = said.IndexOf(Written, StringComparison.Ordinal);
        int end = said.LastIndexOf(" (", StringComparison.Ordinal);
        if (at < 0 || end < at)
        {
            throw new InvalidOperationException($"the export said: {said}");
        }
        string png = said[(at + Written.Length)..end];
        e.Send("click Import…");
        // The picker: its path box (Ctrl+L) gets the picture's path, typed with the pointer over it (no window manager:
        // keys go to the window under the pointer).
        string picker = RunOnDisplay("timeout", "20", "xdotool", "search", "--sync", "--onlyvisible", "--name", "Import a heightmap");
        RunOnDisplay("xdotool", "mousemove", "--window", picker.Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1], "300", "200");
        e.Send("wait 500");
        RunOnDisplay("xdotool", "key", "ctrl+l");
        e.Send("wait 300");
        // Typed behind a folder that is not there ("Q/..."), so the path box has no names to complete it with (it adds
        // what the names have in common: ".cache/vw" became ".cache/vwe-e-tmp"); then the "Q" goes.
        TypeKeys(e, "Q" + png);
        RunOnDisplay("xdotool", "key", "Home", "Delete");
        e.Send("wait 300");
        RunOnDisplay("xdotool", "key", "Return");
        RunOnDisplay("xdotool", "mousemove", "1915", "1075");
        e.Send("wait 1500");
        Camera(e, 10, 0, 200, 60, 90);
        e.Send("wait 500");
        e.Shot(Out("area-heightmap.jpg"));
    }

    /// <summary>A folder copied with everything in it.</summary>
    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string f in Directory.GetFiles(from))
        {
            File.Copy(f, System.IO.Path.Combine(to, System.IO.Path.GetFileName(f)));
        }
        foreach (string d in Directory.GetDirectories(from))
        {
            CopyFolder(d, System.IO.Path.Combine(to, System.IO.Path.GetFileName(d)));
        }
    }

    /// <summary>The Area panel's right edge in the window: halves of its pictures start there.</summary>
    private const int AreaPanelRight = 432;

    /// <summary>Waits (up to 30 s) until the status bar's message begins with these words.</summary>
    private static void WaitForMessage(Editor e, string start)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!(e.Send("wait 300").GetProperty("message").GetString() ?? "").StartsWith(start, StringComparison.Ordinal))
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(30))
            {
                throw new TimeoutException($"no message beginning \"{start}\"");
            }
        }
    }

    /// <summary>
    /// Where <see cref="Halves"/> cuts: round the subject's column in the window, from the panel's right edge, down to
    /// the status bar's top, with parts of the after picture drawn over the halves where they are in the window (a
    /// card, which would show twice otherwise).
    /// </summary>
    /// <param name="Subject">The subject's column in the window, which each half is cut round.</param>
    /// <param name="Left">The window's column where the halves start, right of the tool panel.</param>
    /// <param name="Bottom">The window's row where the halves end (higher for a message on two lines).</param>
    /// <param name="Keep">Parts of the after picture drawn over the halves.</param>
    /// <param name="KeepBefore">Parts of the before picture drawn where they are in the window (a list open in the panel).</param>
    /// <param name="From">The window's column each half is cut from, instead of round the subject.</param>
    private readonly record struct HalvesCut(int Subject, int Left = GroundPairLeft, int Bottom = GroundPairBottom, SKRectI[]? Keep = null, SKRectI[]? KeepBefore = null, int? From = null);

    /// <summary>The Mask card beside the Area panel, kept once over a pair of halves.</summary>
    private static readonly SKRectI MaskCard = new(438, 68, 744, 120);

    /// <summary>
    /// Two pictures of the same view, before and after a change, side by side right of the tool panel: each half is
    /// cut round the subject, labelled, and the rest of the window (panel, bars, the status bar's message) is the after
    /// picture's. The two pictures are deleted.
    /// </summary>
    /// <param name="file">The picture to write.</param>
    /// <param name="labels">The two halves' labels.</param>
    /// <param name="before">The picture before the change.</param>
    /// <param name="after">The picture after it.</param>
    /// <param name="at">Where the halves are cut.</param>
    /// <param name="afterHalf">The picture the after half is cut from, when not <paramref name="after"/> (one without the
    /// brush's ring); deleted too.</param>
    private static void Halves(string file, (string Before, string After) labels, string before, string after, HalvesCut at, string? afterHalf = null)
    {
        try
        {
            using var a = LoadPicture(before);
            using var b = LoadPicture(after);
            using var h = afterHalf == null ? null : LoadPicture(afterHalf);
            using var result = new SKBitmap(b.Width, b.Height);
            using (var canvas = new SKCanvas(result))
            {
                canvas.DrawImage(b, 0, 0, Exact);
                int half = (b.Width - at.Left) / 2;
                int from = at.From ?? Math.Clamp(at.Subject - half / 2, 0, b.Width - half);
                var cut = new SKRect(from, GroundPairTop, from + half, at.Bottom);
                canvas.DrawImage(a, cut, new SKRect(at.Left, GroundPairTop, at.Left + half, at.Bottom), Exact);
                canvas.DrawImage(h ?? b, cut, new SKRect(at.Left + half, GroundPairTop, at.Left + 2 * half, at.Bottom), Exact);
                foreach (var r in at.Keep ?? [])
                {
                    canvas.DrawImage(b, r, r, Exact);
                }
                foreach (var r in at.KeepBefore ?? [])
                {
                    canvas.DrawImage(a, r, r, Exact);
                }
                using var line = new SKPaint { Color = new SKColor(20, 24, 30), StrokeWidth = 3 };
                canvas.DrawLine(at.Left + half, GroundPairTop, at.Left + half, at.Bottom, line);
                GroundLabel(canvas, labels.Before, at.Left + half / 2f);
                GroundLabel(canvas, labels.After, at.Left + half * 1.5f);
            }
            SavePicture(result, file);
        }
        finally
        {
            File.Delete(before);
            File.Delete(after);
            if (afterHalf != null)
            {
                File.Delete(afterHalf);
            }
        }
    }
}
