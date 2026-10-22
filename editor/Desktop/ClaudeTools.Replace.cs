using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>replace_objects: objects become another kind, where they are and turned as they are (as the Area tool's Replace).</summary>
    [McpServerTool(Name = "replace_objects", Title = "Replace objects with another kind")]
    [Description("Replaces objects of the open area (by id) with objects of another kind (a prefab name from find_prefabs), where they are and turned as they are, as the Area tool's Replace does: a birch for a beech, a stone wall for a wooden one, an iron-banded chest for a wooden one. What the old ones held (chest contents, text…) does not go to the new ones; each keeps its size. The root of a group a mod links into one (a ship) takes its parts away with it (partsTakenAway); a part is replaced alone. The answer gives the new ids. One pending step.")]
    public Task<string> ReplaceObjects(
        [Description("Their ids (list_objects).")] int[] ids,
        [Description("The new kind's prefab name, e.g. Birch1, stone_wall_2x1, piece_chest.")] string prefab,
        [Description("Stand each on the ground where it is, its lowest point touching it (default false: same origin height).")] bool onGround = false) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        int hash = Save.StableHash.Of(prefab);
        bool piece = Terrain.PieceCatalog.Get(hash)?.Tool != null;
        if (Terrain.PieceCatalog.Get(hash) == null && Terrain.PrefabCatalog.Get(hash) == null)
        {
            return $"{prefab} is not an object the game has (find_prefabs).";
        }
        var s = _w.Session!;
        var scene = s.Scene;
        int reads = scene.Reads;
        if (StaleIds(scene) is string stale)
        {
            return stale;
        }
        var (ok, problems) = Usable(scene, ids);
        if (ok.Count == 0)
        {
            return NothingDone("Nothing replaced", problems);
        }
        // Each keeps its size; a linked group's root takes its parts away with it (ObjectLinks: a ship), as the Area
        // tool's Replace does: they would have nothing to hang on. A part goes alone.
        var adds = ok.Select(i => scene.Things[i]).Select(t =>
        {
            float y = onGround ? GroundAt(scene, t.Position.X, t.Position.Z) - Editing.Hammer.Bottom(prefab, BlueprintFormats.FromEuler(t.Rotation), t.Scale > 0 ? t.Scale : 1) : t.Position.Y;
            return (new Editing.NewObject(0, hash, t.Position with { Y = y }, t.Rotation, t.Scale), piece);
        }).ToList();
        var (removed, linked, groups, _) = s.WithLinked(ok, anyPart: false);
        int parts = linked.Count - groups;
        var copies = s.Commit(ClaudesStep(Lang.T("claude.undo.replaced", ok.Count, prefab)), null, removed, adds);
        _w.AfterClaudeEdit(Said.Of("claude.msg.replaced", ok.Count, prefab));
        GaveIds(scene, reads);
        return ToJson(new { replaced = copies.Select((c, k) => Moved(scene, ok[k], c)), partsTakenAway = parts, problems, pending = Pending() });
    });
}
