using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// A click in the Paste tool: one paste (with its repeats) is one undo step.
    /// </summary>
    /// <param name="at">The grid point clicked.</param>
    internal void PasteAt(System.Numerics.Vector2 at)
    {
        if (Session is not { } s || View.Paste.Clip == null)
        {
            return;
        }
        // A dungeon blueprint: its entrance where the click is, the pieces 5000 m above.
        if (View.Paste.Clip.Dungeon)
        {
            PasteDungeon(View.Paste.Clip, at);
            return;
        }
        // Past the open area the ground could not be shaped nor the site cleared: the building would stand
        // on whatever is there, among the trees.
        if (!View.Paste.Fits(at, s.Ground.W, s.Ground.H))
        {
            Msg("paste.msg.tooFar");
            return;
        }
        // The objects' heights come from the ground as the paste leaves it: the ground step fills the list,
        // which Commit reads after it (one undo step for both).
        var add = new List<(NewObject, bool)>();
        // Clear the site: the trees, rocks and the like where the pasted building stands go too.
        var cleared = new List<int>();
        int touched = 0, stood = 0;
        var paste = View.Paste;
        string label = paste.Count > 1 ? Lang.T("paste.undo.copies", paste.Count) : Lang.T("paste.label.paste");
        float ox = s.Scene.OriginX, oz = s.Scene.OriginZ;
        var linkIds = s.ReadLinks().Ids;
        // What mods keep with the copy (IAreaCopier): their data holders, in the same step.
        var removeHidden = new List<int>();
        var addHidden = new List<NewObject>();
        s.Commit(label, g =>
        {
            var (t, rect, a) = paste.Apply(g, at, s.MaskNow(), linkIds);
            add.AddRange(a);
            var kept = ModCopies.Put(s, g, rect, paste.Clip, paste.Placed);
            cleared.AddRange(kept.RemoveThings);
            add.AddRange(kept.AddThings);
            removeHidden.AddRange(kept.RemoveHidden);
            addHidden.AddRange(kept.AddHidden);
            stood = CreatureStand.Apply(s.Scene, add);
            touched = t.Count;
            if (paste.Site.Count > 0)
            {
                lock (s.Scene.Things)
                {
                    for (int i = 0; i < s.Scene.Things.Count; i++)
                    {
                        var th = s.Scene.Things[i];
                        if (!th.Gone && EditSession.Natural(th) && paste.Site.Contains(((int)MathF.Round(th.Position.X - ox), (int)MathF.Round(th.Position.Z - oz))))
                        {
                            cleared.Add(i);
                        }
                    }
                }
            }
            return (t, rect);
        }, cleared, add, null, removeHidden, addHidden);
        Msg(PastedSaid(paste.Count, paste.Ground, touched, add.Count, cleared.Count) + CreatureStand.Note(stood) + Said.Of("paste.msg.pasteMore"));
        UpdateSaveBar();
    }

    /// <summary>
    /// What a paste did, for the status bar: the copies, the ground (the copy's own, or with its ground left out only
    /// its site dug out), the objects placed and the nature cleared from the site.
    /// </summary>
    /// <param name="copies">How many copies were pasted.</param>
    /// <param name="ground">Whether the copy's ground was pasted (Ground shape and paint).</param>
    /// <param name="touched">How many ground points changed.</param>
    /// <param name="added">How many objects were placed.</param>
    /// <param name="cleared">How many trees, rocks and the like were cleared from the site.</param>
    internal static Said PastedSaid(int copies, bool ground, int touched, int added, int cleared) =>
        Said.Of("paste.msg.pasted",
            copies > 1 ? Said.Of("paste.msg.pastedCopies", copies) : Said.Empty,
            touched == 0 ? Said.Empty : ground ? Said.Of("paste.msg.pastedGround") : Said.Of("paste.msg.pastedSiteDug"),
            added > 0 ? Said.Of("paste.msg.pastedObjects", added) : Said.Empty,
            cleared > 0 ? Said.Of("paste.msg.pastedCleared", cleared) : Said.Empty);
}
