using System.Numerics;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Blueprints panel's rarer ways: on its own (no world open, nobody listening to what it says) saving, pasting,
/// exporting, importing, deleting and changing details, also into a folder that cannot be written; and in the window,
/// rows without a creator or with a picture that is no picture, older blueprints without a name, a thumbnail that is
/// not a PNG, searches by an older blueprint's name, kinds a world cannot make (more than five), and an import with
/// terrain marks, unreadable lines and unknown kinds.
/// </summary>
public sealed class BlueprintsPanelBranchesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-bp-branches-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Homestead's folder for the panel on its own.</summary>
    private string Folder => Path.Combine(_dir, "homestead");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>A copy with no ground and no name (a wall).</summary>
    private static CopyData Unnamed() => ClipboardSetup.SingleObjectClip(new CopyData.Obj(StableHash.Of("woodwall"), "woodwall", 0, 0, 0, Vector3.Zero, 0, null), []);

    /// <summary>A copy in the clipboard format, without ground (a "none" height and a missing one).</summary>
    private static JsonObject Groundless(params string[] kinds)
    {
        var clip = CopyFormat.ToJson(BlueprintsPanelRun.Clip("x", kinds));
        clip["rel"] = new JsonArray(null, -32768, -32768, -32768, -32768, -32768);
        return clip;
    }

    [AvaloniaFact]
    public async Task OnItsOwnItWorksWithNoWorldAndNobodyListening()
    {
        var paste = new PasteTool();
        var b = new BlueprintsPanel(paste, () => null)
        {
            Store = new BlueprintStore(Path.Combine(_dir, "older")),
            FindHomestead = () => new Homestead.Status(false, null, [], Folder),
            AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d),
        };
        // Nothing copied.
        Assert.Null(await b.Save());
        // A copy without a name: one made from the time; no ground to leave out.
        paste.Clip = Unnamed();
        string name = (await b.Save())!;
        Assert.StartsWith("Blueprint ", name);
        string id = "hs:" + Homestead.FileName(name);
        // Pasted with no world open: every kind kept.
        paste.Clip = null;
        b.Paste(id);
        Assert.Equal(name, paste.Clip!.Name);
        Assert.Single(paste.Clip.Objects);
        // Missing ones.
        b.Paste("no-such-blueprint");
        Assert.Null(b.ToHomestead("no-such-blueprint"));
        Assert.Null(b.Export("no-such-blueprint", "blueprint"));
        Assert.Null(b.Import(Path.Combine(_dir, "missing.vbuild")));
        string empty = Path.Combine(_dir, "empty.vbuild");
        File.WriteAllText(empty, "nothing useful here");
        Assert.Null(b.Import(empty));
        // For PlanBuild.
        string written = b.Export(id, "blueprint")!;
        Assert.EndsWith(".blueprint", written);
        Assert.True(File.Exists(written));
        // Imported: its own name (no world: the game's kinds known).
        Assert.Equal("Wall", b.Import(BlueprintFiles.Vbuild(_dir, "Wall", "woodwall")));
        // Saved over the file it was opened from: not asked; a name taken: asked, and No keeps it.
        var asked = new List<string>();
        b.Confirm = q => { asked.Add(q); return Task.FromResult(false); };
        JsonObject bare = [];
        Assert.NotNull(await b.SaveBuilding(bare, new Homestead.Details("Wall", "", []), Path.Combine(Folder, Homestead.FileName("Wall"))));
        Assert.Empty(asked);
        Assert.Null(await b.SaveBuilding(bare, new Homestead.Details("Wall", "", [])));
        Assert.Single(asked);
        // Deleted: an id outside the folder is left alone; a "file" that is a folder cannot be deleted.
        b.Confirm = _ => Task.FromResult(true);
        await b.Delete("hs:../outside.blueprint", "outside");
        Directory.CreateDirectory(Path.Combine(Folder, "Stuck.blueprint"));
        await b.Delete("hs:Stuck.blueprint", "Stuck");
        Assert.True(Directory.Exists(Path.Combine(Folder, "Stuck.blueprint")));
        // Details of a blueprint whose file went away.
        var entry = Homestead.List(Folder).Single(e => e.Name == "Wall");
        File.Delete(entry.Path);
        b.AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d with { Name = "Wall 2" });
        Assert.False(await b.EditDetails(entry));
        // Shown (switched), then hidden.
        b.Toggle();
        Assert.True(b.Card.IsVisible);
        b.Toggle();
        Assert.False(b.Card.IsVisible);
        // A folder that cannot be written: nothing saved, moved or imported.
        string older = b.Store.Save("Old", null, null, CopyFormat.ToJson(BlueprintsPanelRun.Clip("Old", "woodwall")));
        Directory.Delete(Folder, recursive: true);
        File.WriteAllText(Folder, "not a folder");
        paste.Clip = Unnamed();
        b.AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d with { Name = "Gate" });
        Assert.Null(await b.Save());
        Assert.Null(await b.SaveBuilding(bare, new Homestead.Details("Gate", "", [])));
        Assert.Null(b.ToHomestead(older));
    }

    /// <summary>The panel's list's texts, joined with " | ".</summary>
    private static string Texts(Control c) => string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    [AvaloniaFact]
    public void RowsWithoutACreatorOlderOnesWithoutANameAndWhatAWorldCannotMake()
    {
        using var r = new BlueprintsPanelRun();
        Directory.CreateDirectory(r.Homestead);
        // A Homestead blueprint with no creator, from a world, whose picture is no picture.
        File.WriteAllText(Path.Combine(r.Homestead, "Bare.blueprint"), "#Name:Bare\n#HomesteadWorld:Midgard\n#Pieces\nwoodwall;Building;0;0;0;0;0;0;1;\"\";1;1;1\n");
        File.WriteAllText(Path.Combine(r.Homestead, "Bare.png"), "not a picture");
        // Older ones: a JPEG thumbnail, from another tool, no ground; one with no name at all.
        r.B.Store.Save("Plain", null, "data:image/jpeg;base64,AAAA", Groundless("woodwall"), "PlanBuild");
        Directory.CreateDirectory(r.B.Store.Directory);
        File.WriteAllText(Path.Combine(r.B.Store.Directory, "nameless.json"), new JsonObject { ["clip"] = Groundless("woodwall") }.ToJsonString());
        r.B.Toggle();
        Assert.True(r.B.Card.IsVisible);
        string texts = Texts(r.B.List);
        Assert.Contains("1 piece(s) · from Midgard", texts);
        Assert.Contains("· PlanBuild", texts);
        // No picture from a JPEG thumbnail nor from none: at most Bare's file is read (the headless platform takes
        // any bytes for a picture, a real one would not).
        Assert.InRange(r.B.List.GetLogicalDescendants().OfType<Image>().Count(i => i.Source != null), 0, 1);
        // An older blueprint's name searched: Homestead's are looked through to their tags.
        r.B.Search.Text = "plain";
        Assert.Contains("Plain", Texts(r.B.List));
        Assert.DoesNotContain("Bare", Texts(r.B.List));
        r.B.Search.Text = "";
        // Named after its file; moved to Homestead without ground to leave out.
        r.B.Paste("nameless");
        Assert.Equal("nameless", r.W.View.Paste.Clip!.Name);
        Assert.Equal("nameless", r.B.ToHomestead("nameless"));
        Assert.DoesNotContain("ground shape", r.Said);
        // More than five kinds this world cannot make: the first five, and more.
        string odd = r.B.Store.Save("Odd", null, null, CopyFormat.ToJson(BlueprintsPanelRun.Clip("Odd", "NoSuch_a", "NoSuch_b", "NoSuch_c", "NoSuch_d", "NoSuch_e", "NoSuch_f")));
        r.B.Paste(odd);
        Assert.Contains("6 kind(s) are unknown here and are left out: ", r.Said);
        Assert.EndsWith("….", r.Said);
    }

    [AvaloniaFact]
    public void AnImportSaysWhatItLeftOut()
    {
        using var r = new BlueprintsPanelRun();
        Directory.CreateDirectory(r.Dir);
        // No name (the file is named ".blueprint"), a terrain mark, a line that does not read, six unknown kinds.
        string file = Path.Combine(r.Dir, ".blueprint");
        File.WriteAllText(file, "#Terrain\nsquare;0;0;0;2;0;0;dirt\n#Pieces\nwoodwall;Misc;0;0;0;0;0;0;1;\"\";1;1;1\nbroken\n"
            + string.Concat(Enumerable.Range(0, 6).Select(i => $"NoSuch_{i};Misc;{i};0;0;0;0;0;1;\"\";1;1;1\n")));
        Assert.Equal("Imported", r.B.Import(file));
        Assert.Contains("Its terrain marks are not kept", r.Said);
        Assert.Contains("6 kind(s) the game does not know were left out (mods?): ", r.Said);
        Assert.Contains("….", r.Said);
        Assert.Contains("1 unreadable line(s) skipped.", r.Said);
    }
}
