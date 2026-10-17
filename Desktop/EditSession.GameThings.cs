using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// Live: the game's changed objects taken into the scene, and the area read again after a save.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Takes the game's removed, changed and new objects into the scene's things, listing the indices changed. Under the
    /// scene's things lock.
    /// </summary>
    /// <param name="gone">The ids the game removed (and the ones this session removed).</param>
    /// <param name="now">The objects the world has now, by id, for those changed or new.</param>
    /// <param name="changed">Gets the indices of the things changed or added.</param>
    private void TakeThingChanges(HashSet<int> gone, Dictionary<int, WorldScene.Thing> now, List<int> changed)
    {
        var list = Scene.Things;
        lock (list)
        {
            var at = new Dictionary<int, int>();
            for (int i = 0; i < list.Count; i++)
            {
                at[list[i].Id] = i;
            }
            foreach (int id in gone)
            {
                if (at.TryGetValue(id, out int i) && !list[i].Gone)
                {
                    list[i] = list[i] with { Gone = true };
                    changed.Add(i);
                }
            }
            foreach (var (id, t) in now)
            {
                if (at.TryGetValue(id, out int i))
                {
                    // Changed in the game: shown as it is now (deleted in the editor, it stays so).
                    var next = t with { Gone = list[i].Gone };
                    if (next != list[i])
                    {
                        list[i] = next;
                        changed.Add(i);
                    }
                }
                else if (InsideArea(t.Position) && !t.Gone && t.Prefab != WorldSave.LocationProxyPrefab)
                {
                    changed.Add(list.Count);
                    list.Add(t);
                }
            }
        }
    }

    /// <summary>
    /// The world was read again (saved, reloaded): the ground and the objects from it, no history.
    /// </summary>
    private void Reread(WorldSave fresh)
    {
        Interlocked.Increment(ref _generation);
        Scene.World = fresh;
        // The ground discs may have changed: the generated ground with them, everywhere in the area.
        ReadGround(baseToo: true);
        _undo.Clear();
        _redo.Clear();
        _shelvedUndo.Clear();
        _shelvedRedo.Clear();
        // Saving gives objects new ids: read them again.
        var things = WorldScene.ReadThings(fresh, Scene.X0, Scene.Z0, Scene.ZonesX, Scene.ZonesZ, Edits.Deleted, Edits.Added);
        lock (Scene.Things)
        {
            Scene.Things.Clear();
            Scene.Things.AddRange(things);
            Scene.Reads++;
        }
        _nextId = -1;
    }
}
