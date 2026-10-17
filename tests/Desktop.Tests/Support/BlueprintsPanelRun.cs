using System.Numerics;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A main window with a flat area (or the session given), its Blueprints panel pointed at a temporary folder for
/// Homestead's and the older blueprints, and whether Homestead is installed, its questions answered Yes; the folder is
/// deleted when disposed.
/// </summary>
internal sealed class BlueprintsPanelRun : IDisposable
{
    public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "vwe-bp-ui-" + Guid.NewGuid().ToString("N")[..8]);
    public string Homestead => Path.Combine(Dir, "homestead");
    public BlueprintsPanel B => W.Blueprints;
    public string? Said;
    /// <summary>The panel's last message in English (what Claude reads).</summary>
    public string? SaidEn;
    public bool Installed { get; set; } = true;

    public BlueprintsPanelRun(EditSession? s = null)
    {
        W.Show();
        s ??= TestScenes.Flat(2);
        W.View.Show(s.Scene, null);
        W.Edit(s);
        B.Store = new BlueprintStore(Path.Combine(Dir, "older"));
        B.FindHomestead = () => new Homestead.Status(Installed, Installed ? "1.3.2" : null, Installed ? new() { "test profile" } : new(), Homestead);
        B.Message += t =>
        {
            Said = t.Shown;
            SaidEn = t.English;
        };
        // Its questions answered Yes, not asked in a dialog: headless, nobody would answer it and the test would wait
        // for ever (a test that wants No says so).
        B.Confirm = _ => Task.FromResult(true);
    }

    /// <summary>
    /// A Homestead blueprint in the folder.
    /// </summary>
    public void Keep(string name, params string[] kinds)
    {
        Directory.CreateDirectory(Homestead);
        File.WriteAllText(Path.Combine(Homestead, TerrainEditor.App.Homestead.FileName(name)), TerrainEditor.App.Homestead.Write(CopyFormat.ToJson(Clip(name, kinds)), name, "test", null, DateTime.Now));
    }

    /// <summary>
    /// The window's Workshop opened, blank or with the blueprint at <paramref name="path"/>; the window's questions
    /// answered Yes and what it tells let go, like the panel's.
    /// </summary>
    /// <param name="path">The blueprint to open, or null for a blank plot.</param>
    public async Task<MainWindow> OpenWorkshop(string? path = null)
    {
        W.Ask = (_, _, _, _) => Task.FromResult(true);
        W.Tell = _ => Task.CompletedTask;
        await W.OpenWorkshop(path);
        return W;
    }

    public List<Homestead.Entry> Listed => TerrainEditor.App.Homestead.List(Homestead);

    /// <summary>
    /// The buttons with this label in the panel's list (one per row that has it).
    /// </summary>
    public List<Button> RowButtons(string label) => [.. B.List.GetLogicalDescendants().OfType<Button>().Where(b => b.Content as string == label)];

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, recursive: true);
        }
    }

    /// <summary>
    /// A small 3 × 2 blueprint named <c>name</c> holding one object of each kind given.
    /// </summary>
    internal static CopyData Clip(string name, params string[] kinds) => new()
    {
        W = 3,
        H = 2,
        Rel = [0f, 0, 0, 0, 0, 0],
        Wt = [1f, 1, 1, 1, 1, 1],
        Pnt = [.. Enumerable.Repeat(-1f, 24)],
        Objects = [.. kinds.Select((k, i) => new CopyData.Obj(StableHash.Of(k), k, i, 0, 0, Vector3.Zero, 0, null))],
        Poly = [new(-2, -1), new(2, -1), new(2, 1), new(-2, 1)],
        Name = name,
    };
}
