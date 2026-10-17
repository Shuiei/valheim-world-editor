using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public static partial class WorldWriter
{
    /// <summary>
    /// Plans the zone edits: a zone with terrain objects gets the new ground in them; a zone without one gets a new
    /// terrain object, copied from the world's template terrain object (when the world has one in the short format).
    /// </summary>
    internal static void PlanTerrainEdits(WorldSave world, Planned plan, Dictionary<(int, int), List<TerrainZone>> existing, Func<int, int, ChunkFile?> findChunk)
    {
        // New ground copies a terrain object whose position is in the short format (the layout the copy patches).
        bool anyTerrain = world.TerrainZones.Any(z => z.Source != null);
        TerrainZone? template = world.TerrainZones.FirstOrDefault(z => z.Source != null && (z.Source.Flags & ZdoData.SmallPositionFlag) != 0);
        // The template terrain object's own bytes, read from its chunk file at the first new ground.
        byte[]? templateBytes = null;
        foreach (ZoneEdit edit in plan.Changed)
        {
            byte[] data = EncodeTerrain(edit);
            if (existing.TryGetValue((edit.ZoneX, edit.ZoneZ), out List<TerrainZone>? zones))
            {
                foreach (TerrainZone zone in zones)
                {
                    (plan.Patches.TryGetValue(zone.Source!.File, out var p) ? p : plan.Patches[zone.Source.File] = [])[zone.Source.Start] = (zone.Source, data);
                }
                plan.Written++;
                continue;
            }
            if (edit.HeightCount + edit.PaintCount == 0)
            {
                plan.NotSaved.Add((edit.ZoneX, edit.ZoneZ));
                continue;
            }
            ChunkFile? target = findChunk(edit.ZoneX, edit.ZoneZ);
            if (target == null)
            {
                plan.Skipped.Add(ChunkMath.InWorld(edit.ZoneX, edit.ZoneZ) ? $"zone {edit.ZoneX}, {edit.ZoneZ}: not generated yet (no world data there; visit it in game first)" : $"zone {edit.ZoneX}, {edit.ZoneZ}: outside the world");
                plan.NotSaved.Add((edit.ZoneX, edit.ZoneZ));
                continue;
            }
            if (template == null)
            {
                plan.Skipped.Add(anyTerrain
                    ? $"zone {edit.ZoneX}, {edit.ZoneZ}: the world's terrain objects are in an unexpected layout (position not in the short format), so none can be copied"
                    : $"zone {edit.ZoneX}, {edit.ZoneZ}: the world has no terrain object to copy yet (edit the ground once in game first)");
                plan.NotSaved.Add((edit.ZoneX, edit.ZoneZ));
                continue;
            }
            templateBytes ??= TemplateBytes(world, template.Source!);
            (plan.Additions.TryGetValue(target, out var a) ? a : plan.Additions[target] = []).Add(NewTerrainObject(template.Source!, templateBytes, edit, data));
            plan.Created++;
        }
    }
}
