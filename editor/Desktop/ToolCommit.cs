using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.Desktop;

/// <summary>
/// A mod's tool's edit (ToolEdit) made one step of an area's history: its added objects made with the data the tool
/// sets, its changed ones replaced by a copy with the tool's changes (same prefab, place and turn, its source the old
/// one), its removed ones taken away; things for objects the scene shows, hidden objects for mods' data holders.
/// </summary>
internal static class ToolCommit
{
    /// <summary>What an edit comes to: things and hidden objects to take away, and new ones of each.</summary>
    internal sealed record Plan(List<int> RemoveThings, List<(NewObject, bool)> AddThings, List<int> RemoveHidden, List<NewObject> AddHidden, List<string> Skipped);

    /// <summary>Makes an edit one step of the session's history; what it could not do is listed (an object not from the area).</summary>
    /// <param name="s">The area's session.</param>
    /// <param name="label">The step's name.</param>
    /// <param name="edit">The edit.</param>
    public static Plan Apply(EditSession s, string label, ToolEdit edit)
    {
        var plan = PlanOf(s, edit);
        s.CommitObjects(label, plan.RemoveThings, plan.AddThings, plan.RemoveHidden, plan.AddHidden);
        return plan;
    }

    /// <summary>What an edit comes to in a session.</summary>
    internal static Plan PlanOf(EditSession s, ToolEdit edit) => AddTo(new Plan([], [], [], [], []), s, edit);

    /// <summary>Adds what an edit comes to in a session to a plan.</summary>
    /// <param name="plan">The plan, added to.</param>
    /// <param name="s">The area's session.</param>
    /// <param name="edit">The edit.</param>
    internal static Plan AddTo(Plan plan, EditSession s, ToolEdit edit)
    {
        foreach (var (prefab, position, rotation, data) in edit.Added)
        {
            int hash = StableHash.Of(prefab);
            var z = new ZdoData { Prefab = hash, BaseFlags = SavedFlags(hash) };
            data?.Invoke(new DataCopy(prefab, position, rotation, z));
            AddNew(plan, new NewObject(0, z.Prefab, position, rotation, 1, Fresh: false, Raw: z.Serialize()));
        }
        foreach (var (obj, change) in edit.Changed)
        {
            if (Remove(s, plan, obj) is not { } view)
            {
                continue;
            }
            var z = ZdoData.Parse(view.Data.Serialize());
            change(new DataCopy(view.Prefab, view.Position, view.Rotation, z, view.Scale));
            AddNew(plan, new NewObject(0, z.Prefab, view.Position, view.Rotation, view.Scale, view.Id, Fresh: false, Raw: z.Serialize()));
        }
        foreach (var obj in edit.Removed)
        {
            Remove(s, plan, obj);
        }
        return plan;
    }

    /// <summary>The persistent flag of an object's save (the game keeps such objects, and saves them).</summary>
    private const ushort Persistent = 0x100;

    /// <summary>
    /// The save flags of a new object of a prefab: the game's own for a prefab it has (persistent, distant, its type);
    /// persistent for one it does not list, as a mod's data holder made at run time is: a new object the game did not
    /// keep would be taken away again, once its zone is no longer loaded, and the mod's data with it.
    /// </summary>
    private static ushort SavedFlags(int prefab) => PrefabCatalog.Details(prefab)?.Flags ?? Persistent;

    /// <summary>A new object: a hidden one for a mod's data holder, else a thing (a piece when the build tools make it).</summary>
    private static void AddNew(Plan plan, NewObject n)
    {
        if (ModManifests.IsData(n.Prefab))
        {
            plan.AddHidden.Add(n);
        }
        else
        {
            plan.AddThings.Add((n, PieceCatalog.Get(n.Prefab)?.Tool != null));
        }
    }

    /// <summary>
    /// Takes an object of the area away: by its id, among the hidden objects or the scene's things. Null (listed as
    /// skipped) for one the editor did not hand over (no id) or no longer has.
    /// </summary>
    private static ObjectView? Remove(EditSession s, Plan plan, IObjectView obj)
    {
        if (obj is not ObjectView { Id: int id } view)
        {
            plan.Skipped.Add(obj?.Prefab ?? "");
            return null;
        }
        if (ModManifests.IsData(StableHash.Of(view.Prefab)) || view.Data.Prefab != 0 && ModManifests.IsData(view.Data.Prefab))
        {
            plan.RemoveHidden.Add(id);
            return view;
        }
        int index;
        lock (s.Scene.Things)
        {
            index = s.Scene.Things.FindLastIndex(t => t.Id == id && !t.Gone);
        }
        if (index < 0)
        {
            plan.Skipped.Add(view.Prefab);
            return null;
        }
        plan.RemoveThings.Add(index);
        return view;
    }
}
