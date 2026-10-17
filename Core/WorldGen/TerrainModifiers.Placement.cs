using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

public sealed partial class TerrainModifiers
{
    /// <summary>
    /// The modifiers of a world's placed locations and objects.
    /// </summary>
    public TerrainModifiers(WorldSave world) : this(world.Placed)
    {
    }

    /// <summary>
    /// From any set of placed objects (Uplift: the world's with ground discs added or taken away).
    /// </summary>
    public TerrainModifiers(IEnumerable<PlacedObject> placed)
    {
        List<Modifier> all = [];
        foreach (PlacedObject p in placed)
        {
            int key = p.Location != 0 ? p.Location : p.Prefab;
            if (!_prefabs.TryGetValue(key, out var prefab) || prefab.Location != (p.Location != 0))
            {
                continue;
            }
            if (prefab.Location)
            {
                LocationsWithModifiers++;
            }
            // Quaternion.Euler in Unity applies z, then x, then y: the same as yaw/pitch/roll here.
            var rot = Quaternion.CreateFromYawPitchRoll(Deg(p.Rotation.Y), Deg(p.Rotation.X), Deg(p.Rotation.Z));
            foreach (Template t in prefab.Mods)
            {
                Vector3 pos = p.Position + Vector3.Transform(t.Offset, rot);
                all.Add(new Modifier(pos, t.Level, t.LevelOffset, t.LevelRadius, t.Square, t.Smooth, t.SmoothRadius, t.SmoothPower, t.PaintCleared, t.PaintRadius, t.PaintType, t.PaintStrength, t.PaintHeightCheck, t.SortOrder, t.Player, prefab.Location ? 0 : p.TimeCreated,
                    prefab.Location && Editing.Uplift.IsDisc(p.Location)));
            }
        }
        SortByModifiers(all);
        IndexByZone(all);
        Count = all.Count;
    }
}
