using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>An older editor blueprint as a Homestead blueprint (objects only).</summary>
    /// <returns>The name it was saved under (numbered when taken), null when not saved.</returns>
    public string? ToHomestead(string id)
    {
        if (Load(id, out Said? problem) is not var (clip, name, world, _))
        {
            Message?.Invoke(problem ?? Said.Of("blueprints.msg.unreadable"));
            return null;
        }
        string target = Homestead.FreeName(name, HomesteadExists);
        if (!TryWrite(() => WriteHomestead(clip, Named(target), world)))
        {
            return null;
        }
        Message?.Invoke(Said.Of("blueprints.msg.movedToHomestead", target) + GroundNotKept(clip) + Reminder);
        Refresh();
        return target;
    }

    /// <summary>Deletes a blueprint after asking: a Homestead one with its picture, or an older one from its store.</summary>
    public async Task Delete(string id, string name)
    {
        if (!await Confirm(Lang.T("blueprints.ask.delete.text", name)))
        {
            return;
        }
        if (id.StartsWith(HomesteadId, StringComparison.Ordinal))
        {
            string path = Path.Combine(Status.Folder, id[HomesteadId.Length..]);
            if (Path.GetFileName(path) == id[HomesteadId.Length..] && !TryWrite(() =>
            {
                File.Delete(path);
                File.Delete(Path.ChangeExtension(path, ".png"));
            }))
            {
                return;
            }
        }
        else
        {
            Store.Delete(id);
        }
        Refresh();
    }
}
