using System.Globalization;
using TerrainEditor.Save;

/// <summary>The verify-ingame check: the editor's ground against the heightmaps that the TerrainCheck plugin recorded in the game.</summary>
internal static class IngameVerifyCheck
{
    /// <summary>Compares each recorded zone with the editor's ground and prints how far apart they are; returns the exit code.</summary>
    /// <param name="args">verify-ingame, the world folder, then the TerrainCheck file.</param>
    /// <param name="world">The world whose saved edits are combined with the ground.</param>
    /// <param name="terrain">The editor's terrain for the world.</param>
    /// <param name="modifiers">The world's terrain modifiers, counted per zone in the report.</param>
    internal static int Run(string[] args, WorldSave world, ValheimGen.TerrainService terrain, TerrainEditor.Terrain.TerrainModifiers modifiers)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("verify-ingame needs the TerrainCheck file");
            return 2;
        }
        // Compare the editor's ground (generated + location flattening + saved edits, combined like
        // TerrainComp.ApplyToHeightmap) with heightmaps recorded in the game by the TerrainCheck plugin.
        string[] lines = File.ReadAllLines(args[2]);
        Console.WriteLine(lines.FirstOrDefault(l => l.StartsWith('#')) ?? "");
        int zonesExact = 0, zonesClose = 0, zonesOff = 0, rawBetter = 0;
        double maxWith = 0, maxWithout = 0;
        var report = new List<string>();
        foreach (string line in lines.Where(l => l.StartsWith("Z ")))
        {
            string[] p = line.Split(' ');
            int zx = int.Parse(p[1]), zz = int.Parse(p[2]);
            float[] game = [.. p.Skip(3).Select(h => BitConverter.Int32BitsToSingle(int.Parse(h, NumberStyles.HexNumber)))];
            var zone = world.TerrainZones.FirstOrDefault(t => t.ZoneX == zx && t.ZoneZ == zz);
            float[] with = WithSavedEdits(zone, terrain.BaseZone(zx, zz)), without = WithSavedEdits(zone, terrain.RawZone(zx, zz));
            double dWith = game.Zip(with, (a, b) => (double)Math.Abs(a - b)).Max(), dWithout = game.Zip(without, (a, b) => (double)Math.Abs(a - b)).Max();
            maxWith = Math.Max(maxWith, dWith); maxWithout = Math.Max(maxWithout, dWithout);
            int mods = modifiers.InZone(zx, zz).Count;
            if (dWith < 1e-4) { zonesExact++; } else if (dWith < 0.01) { zonesClose++; } else { zonesOff++; report.Add($"  zone ({zx},{zz}): max diff {dWith:0.000} m with flattening, {dWithout:0.000} m without; {mods} modifiers, edits: {zone != null}"); }
            if (dWithout > dWith + 1e-3)
            {
                rawBetter++;
            }
        }
        Console.WriteLine($"Zones compared: {zonesExact + zonesClose + zonesOff}: {zonesExact} exact, {zonesClose} within 1 cm, {zonesOff} off");
        Console.WriteLine($"Max difference: {maxWith:0.0000} m with location flattening, {maxWithout:0.0000} m without ({rawBetter} zones improved by the flattening)");
        report.Take(30).ToList().ForEach(Console.WriteLine);
        return 0;
    }

    /// <summary>A copy of the ground with the zone's saved edits added to each point, each change kept within 8 m of the ground.</summary>
    /// <param name="zone">The zone's saved edits, or null when the zone has none.</param>
    /// <param name="ground">The heights of the zone's ground, one per point.</param>
    private static float[] WithSavedEdits(TerrainZone? zone, float[] ground)
    {
        float[] r = (float[])ground.Clone();
        if (zone != null)
        {
            for (int i = 0; i < r.Length; i++)
            {
                float l = zone.LevelDelta[i], sm = zone.SmoothDelta[i];
                if (l != 0f || sm != 0f)
                {
                    r[i] = Math.Clamp(ground[i] + l + sm, ground[i] - 8f, ground[i] + 8f);
                }
            }
        }
        return r;
    }
}
