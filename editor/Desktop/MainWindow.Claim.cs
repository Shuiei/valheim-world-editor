using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Make player built: the selected pieces players build that have no builder get the chosen one.
    /// </summary>
    internal void Claim()
    {
        View.SelectTool.Commit();
        if (Session is not { Scene.World: { } world } s)
        {
            return;
        }
        if (TerrainEditor.Save.WorldSave.Builder == 0)
        {
            Msg("select.msg.claimNobody");
            return;
        }
        var sel = View.Selected.ToList();
        var remove = new List<int>();
        var adds = new List<(NewObject, bool)>();
        foreach (int i in sel)
        {
            var t = s.Scene.Things[i];
            if (ObjectData.Bytes(world, s.Edits, t.Id) is { } bytes && Builders.Claimed(bytes, TerrainEditor.Save.WorldSave.Builder) is { } z)
            {
                remove.Add(i);
                adds.Add((new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize()), true));
            }
        }
        if (adds.Count == 0)
        {
            Msg("select.msg.claimNothing");
            return;
        }
        var copies = s.Commit(Lang.T("select.undo.claimed", adds.Count), null, remove, adds);
        View.Select(sel.Where(i => !remove.Contains(i)).Concat(copies));
        Msg("select.msg.claimed", adds.Count, sel.Count - adds.Count);
        UpdateSaveBar();
    }

    /// <summary>
    /// The inspector on the one selected object (the View panel makes room for it).
    /// </summary>
    internal void Inspect()
    {
        View.SelectTool.Commit();
        if (View.Selected.Count == 1 && Session?.Scene.Things[View.Selected.First()].Runestone == true)
        {
            Msg("select.msg.runestoneData");
            return;
        }
        if (View.Selected.Count == 1 && Inspector.Open(View.Selected.First()))
        {
            ViewPanelCard.IsVisible = false;
        }
    }
}
