using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// Replaces an object by a copy with these values set (null takes one away) and these contents (null: as they
    /// are), as one pending step: from its saved bytes in a world, from its values on the Workshop's plot.
    /// </summary>
    /// <param name="id">The object's index into the scene's things.</param>
    /// <param name="set">Values to set or remove.</param>
    /// <param name="inv">The container's new contents, or null.</param>
    /// <param name="label">The undo step's name.</param>
    /// <param name="notes">Told of stacks cut to the item's maximum, when given.</param>
    /// <returns>The copy's id, or why nothing changed.</returns>
    internal (int? Id, string? Problem) Rewrite(int id, List<FieldChange> set, List<ItemUpload>? inv, string label, ICollection<string>? notes = null)
    {
        var s = _w.Session!;
        int reads = s.Scene.Reads;
        var t = s.Scene.Things[id];
        try
        {
            Editing.NewObject replacement;
            // (The Workshop's plot has no world files behind it: its objects carry their values.)
            if (!_w.InWorkshop && s.Scene.World is { } world && ObjectData.Bytes(world, s.Edits, t.Id) is { } bytes)
            {
                // A world's object: its own data, changed (as the inspector does).
                var z = ObjectData.Edited(bytes, set, inv, notes);
                replacement = new(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize());
            }
            else
            {
                // On the Workshop's plot: its values, with these.
                var fields = (s.Edits.FindAdded(t.Id)?.Data ?? []).ToList();
                void Put(string section, int key, string? value)
                {
                    fields.RemoveAll(f => f.Key == key);
                    if (value != null)
                    {
                        fields.Add(new(section, key, value));
                    }
                }
                foreach (var f in set)
                {
                    Put(f.Section, Terrain.ZdoKeys.Parse(f.Key), f.Value);
                }
                if (inv != null)
                {
                    Put("bytes", ObjectData.ItemsKey, Convert.ToBase64String(ObjectData.BuildInventory(inv, notes).Write()));
                    Put("ints", ObjectData.AddedDefaultItemsKey, "1");
                }
                replacement = new(0, t.Prefab, t.Position, t.Rotation, t.Scale, Data: fields);
            }
            var copies = s.Commit(label, null, [id], [(replacement, t.Piece)]);
            GaveIds(s.Scene, reads);
            return (copies[0], null);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            Log.CaughtBrief(ex, "setting the object data Claude gave");
            return (null, $"Could not change it: {ex.Message}");
        }
    }
}
