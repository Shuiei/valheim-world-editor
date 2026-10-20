using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// The tag of a dungeon's portal pair from a number (its seed, its name's hash): "dg" and the last five digits of
    /// its size, never negative (a negative seed) nor failing (a hash of int.MinValue has no int size).
    /// </summary>
    internal static string PortalTag(long n) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"dg{Math.Abs(n) % 100000}");

    /// <summary>The tags a generated dungeon's blueprint is written with: a dungeon (pasted, it goes up behind an entrance), generated.</summary>
    private static readonly string[] DungeonTags = [Homestead.Details.DungeonTag, "generated"];

    /// <summary>
    /// A generated dungeon as a blueprint (in the game's Homestead folder), opened in the Workshop. What
    /// its objects hold (the key in its chest, signs, the boss's stars) goes in the editor's own lines of
    /// the file, and the building is written as a ruin (no builder): the editor puts them back when the
    /// blueprint is placed in a world. Built in game with Homestead, it is only the objects.
    /// </summary>
    internal async Task OpenGeneratedInWorkshop(DungeonGen.Settings settings)
    {
        if (await WriteGeneratedBlueprint(settings, DungeonGen.Make(settings)) is string path)
        {
            await OpenWorkshop(path);
        }
    }

    /// <summary>
    /// The Workshop's Generate dungeon: the dungeon made onto the plot, alone (asking first when what is
    /// there is not saved) or added to it in its middle (not when the plot changed while its blueprint was
    /// written: said). Its blueprint stays in the Library.
    /// </summary>
    internal async Task GeneratedOntoPlot(DungeonGen.Settings settings, DungeonGen.Result r, bool add)
    {
        // The plot it is added to: another one opened (or the Workshop left) while the blueprint was written does not
        // get it.
        var plot = Session;
        if (await WriteGeneratedBlueprint(settings, r) is not string path)
        {
            return;
        }
        BuildPanel.RenderLibrary();
        if (add)
        {
            if (!InWorkshop || Session != plot)
            {
                Msg("dungeon.msg.workshopChanged");
                return;
            }
            AddToWorkshop(path, null);
        }
        else
        {
            await OpenWorkshop(path);
        }
    }

    /// <summary>
    /// A generated dungeon written as a blueprint, tagged a dungeon, with a portal where a player arrives in it (its
    /// mark: pasted into a world, the entrance's door out opens there); its file, or null when it could not be written
    /// (said).
    /// </summary>
    private async Task<string?> WriteGeneratedBlueprint(DungeonGen.Settings settings, DungeonGen.Result r)
    {
        try
        {
            // The Library's folder (Homestead's).
            string folder = Blueprints.Status.Folder;
            Directory.CreateDirectory(folder);
            // A blueprint of that name already (one changed in the Workshop, perhaps): kept, this one numbered.
            string path = Path.Combine(folder, Homestead.FileName(Homestead.FreeName(r.Name, n => File.Exists(Path.Combine(folder, Homestead.FileName(n))))));
            await WriteBlueprintFile(path, GeneratedBlueprint(settings, r));
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "writing a generated dungeon's blueprint into the Library");
            Msg("dungeon.msg.notWritten", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Claude's generate_dungeon in the Workshop: the dungeon added to what is on the plot, in its middle, as one step
    /// named <paramref name="label"/>. Its blueprint is not kept: the Library is the user's (Claude never writes it).
    /// </summary>
    internal void AddGeneratedToPlot(DungeonGen.Settings settings, DungeonGen.Result r, string label) =>
        AddToWorkshop(Homestead.FileName(r.Name), null, label: label, text: GeneratedBlueprint(settings, r));

    /// <summary>
    /// A generated dungeon as a Homestead blueprint's text (what <see cref="WriteGeneratedBlueprint"/> writes).
    /// </summary>
    private static string GeneratedBlueprint(DungeonGen.Settings settings, DungeonGen.Result r)
    {
        var items = r.Items.Append(new DungeonGen.Item("portal_wood", r.Arrival, Quaternion.Identity)).ToList();
        float mx = (items.Min(i => i.Position.X) + items.Max(i => i.Position.X)) / 2, mz = (items.Min(i => i.Position.Z) + items.Max(i => i.Position.Z)) / 2;
        float my = items.Min(i => i.Position.Y);
        var objects = new System.Text.Json.Nodes.JsonArray();
        foreach (var it in items)
        {
            var e = BlueprintFormats.ToEuler(it.Rotation);
            var data = it.Values().Append(ObjectField.NoBuilder);
            objects.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = it.Prefab,
                ["dx"] = it.Position.X - mx,
                ["dy"] = it.Position.Y - my,
                ["dz"] = it.Position.Z - mz,
                ["rx"] = e.X,
                ["ry"] = e.Y,
                ["rz"] = e.Z,
                ["scale"] = 0,
                ["data"] = BlueprintFormats.DataJson(data),
            });
        }
        return Homestead.Write(new System.Text.Json.Nodes.JsonObject { ["objects"] = objects }, r.Name, "Valheim World Editor", null, DateTime.Now,
            $"A generated dungeon (seed {settings.Seed}): {Said.Join(" ", r.Notes).English.TrimEnd('.')}. The portal marks where players arrive: the entrance's door out opens there.", DungeonTags);
    }
}
