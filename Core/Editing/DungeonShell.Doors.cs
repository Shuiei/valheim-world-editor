using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal static partial class DungeonShell
{
    /// <summary>A doorway: 4 m of wall with its opening and what closes it.</summary>
    internal static void Doorway(DungeonPlan.Doorway d, float f, Material mat, DungeonOut o, string biome)
    {
        var (cx, cz) = d.Center;
        bool alongZ = d.Dir.X != 0;
        float line = alongZ ? cx : cz, c = alongZ ? cz : cx, yaw = alongZ ? 90 : 0;
        float h = f + Math.Max(DungeonPlan.HeightOf(d.A), DungeonPlan.HeightOf(d.B));
        var site = new DoorSite(mat, o, alongZ, line, c, f, h, yaw);
        switch (d.Kind)
        {
            case Door.Open:
                site.Open();
                break;
            case Door.Grand:
                site.Grand();
                break;
            case Door.Wood:
                site.Frame(3);
                o.Add("dungeon_forestcrypt_door", site.At(c, f), yaw);
                break;
            case Door.Gate:
                site.Frame(3);
                o.Add(biome == "Ashlands" ? "Morkborg_gate" : biome == "Mountain" ? "wood_gate" : "dungeon_sunkencrypt_irongate", site.At(c, f + (biome == "Mountain" ? 1.03f : 0)), yaw);
                break;
            case Door.Key:
                site.Frame(3);
                o.Add("sunken_crypt_gate", site.At(c, f), yaw);
                break;
            case Door.Grate:
                site.Frame(3);
                o.Add("iron_grate", site.At(c, f + 1), yaw);
                break;
            case Door.Curtain:
                site.Frame(4);
                o.Add("cloth_hanging_door", site.At(c, f - 0.25f), yaw);
                break;
            case Door.Secret:
                site.Secret();
                break;
            default:
                break;
        }
    }
}
