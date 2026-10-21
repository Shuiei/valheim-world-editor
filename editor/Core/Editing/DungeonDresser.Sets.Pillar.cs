
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A column where the floor is free (not in a doorway's way, nor on the loft's stairs); nothing otherwise.</summary>
    private void Pillar(Frame f, float u, float v)
    {
        if (f.Free(u, v, 0.8f))
        {
            Column(f, u, v);
        }
    }

    /// <summary>The kinds of cluster that suit the style's halls, crossings and entrance.</summary>
    private string[] StyleClusters => _style.Name switch
    {
        "Crypt" or "Catacombs" => ["bones", "altar"],
        "Temple" => ["altar", "scribe"],
        "Fortress" or "Prison" => ["armoury", "meal"],
        "Dvergr hold" => ["stores", "workshop"],
        "Goblin warren" => ["camp", "bones"],
        _ => ["bones", "stores"],
    };

    /// <summary>A post of the material from the floor to h metres up at (u, v), keeping the floor round it.</summary>
    private void Post(Frame f, float u, float v, float h)
    {
        var p = f.W(u, v, 0);
        DungeonShell.Stack(_mat.Column[0], _o, p.X, p.Z, f.F, f.F + h);
        f.Take(u, v, 0.8f);
    }
}
