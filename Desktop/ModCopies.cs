using System.Numerics;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.Desktop;

/// <summary>
/// What mods' extensions keep of a copied place (IAreaCopier): taken when the copy is made, put down with its paste in
/// the paste's undo step.
/// </summary>
internal static class ModCopies
{
    /// <summary>What the running extensions keep of a copy's place, by copier name; none when no extension copies.</summary>
    /// <param name="s">The scene copied from.</param>
    /// <param name="clip">The copy, its middle and outline where it was made.</param>
    public static Dictionary<string, byte[]> Keep(WorldScene s, CopyData clip)
    {
        if (!ModExtensions.AnyCopiers)
        {
            return [];
        }
        Vector2[] outline = [.. clip.Poly.Select(p => new Vector2(clip.Middle.X + p.X, clip.Middle.Z + p.Y))];
        var region = new CopyRegion(clip.Middle, outline, (x, z) => GroundCuts.Inside(outline, x, z));
        return ModExtensions.CopyArea(new SceneLayers.AreaView(s), region);
    }

    /// <summary>
    /// Puts down what a pasted copy kept, where the paste put each copy (PasteTool.Placed): the copiers' edits as one
    /// plan (ToolCommit), read against the ground as the paste leaves it; an empty one when the copy kept nothing.
    /// </summary>
    /// <param name="s">The area's session.</param>
    /// <param name="g">The ground, as the paste leaves it.</param>
    /// <param name="rect">The grid rectangle the paste changed, brought to the scene first.</param>
    /// <param name="clip">The copy pasted.</param>
    /// <param name="copies">Where each copy landed.</param>
    public static ToolCommit.Plan Put(EditSession s, Ground g, (int X0, int Z0, int X1, int Z1) rect, CopyData clip, IReadOnlyList<CopyPlacement> copies)
    {
        var plan = new ToolCommit.Plan([], [], [], [], []);
        if (clip.Kept.Count == 0 || copies.Count == 0)
        {
            return plan;
        }
        // The extensions read the ground as the paste leaves it; the scene takes it once the step ends.
        s.Scene.Refresh(g, Math.Max(0, rect.X0), Math.Max(0, rect.Z0), Math.Min(g.W - 1, rect.X1), Math.Min(g.H - 1, rect.Z1));
        var unread = new List<string>();
        foreach (var edit in ModExtensions.PasteArea(new SceneLayers.AreaView(s.Scene), clip.Kept, copies, unread))
        {
            ToolCommit.AddTo(plan, s, edit);
        }
        foreach (string name in unread)
        {
            Console.WriteLine($"paste: what {name} kept with the copy was left out: no extension running reads it");
        }
        return plan;
    }
}
