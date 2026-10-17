using System.Numerics;

namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>Takes a float entry: the scale when it is the object's scalar scale.</summary>
    /// <param name="f">The fields being filled.</param>
    /// <param name="zdo">The reader positioned at the entry.</param>
    /// <param name="key">The entry's key.</param>
    private static void ReadFloatField(ZdoFields f, ZdoReader zdo, int key)
    {
        if (key == ZdoData.ScaleScalarKey)
        {
            f.Scale = new Vector3(zdo.FloatValue);
        }
    }

    /// <summary>Takes a vector entry: the scale, or a mod link's vector.</summary>
    /// <param name="f">The fields being filled.</param>
    /// <param name="zdo">The reader positioned at the entry.</param>
    /// <param name="key">The entry's key.</param>
    private static void ReadVec3Field(ZdoFields f, ZdoReader zdo, int key)
    {
        if (key == ZdoData.ScaleKey)
        {
            f.Scale = zdo.Vec3Value;
        }
        else if (f.Links.Vec3Keys.Contains(key))
        {
            (f.LinkVec3s ??= []).Add((key, zdo.Vec3Value));
        }
    }

    /// <summary>Takes a quaternion entry, when a mod links objects by it.</summary>
    /// <param name="f">The fields being filled.</param>
    /// <param name="zdo">The reader positioned at the entry.</param>
    /// <param name="key">The entry's key.</param>
    private static void ReadQuatField(ZdoFields f, ZdoReader zdo, int key)
    {
        if (f.Links.QuatKeys.Contains(key))
        {
            (f.LinkQuats ??= []).Add((key, zdo.QuatValue));
        }
    }

    /// <summary>Takes an int entry: a location proxy's location, the tamed flag, or a mod link's int.</summary>
    /// <param name="site">The object.</param>
    /// <param name="f">The fields being filled.</param>
    /// <param name="zdo">The reader positioned at the entry.</param>
    /// <param name="key">The entry's key.</param>
    private void ReadIntField(ZdoSite site, ZdoFields f, ZdoReader zdo, int key)
    {
        if (site.Prefab == LocationProxyPrefab && key == LocationKey)
        {
            Locations.Add((site.Position, zdo.IntValue));
            f.Location = zdo.IntValue;
        }
        else if (key == TamedKey && zdo.IntValue != 0)
        {
            Tamed.Add(site.Id);
        }
        else if (f.Links.IntKeys.Contains(key))
        {
            (f.LinkInts ??= []).Add((key, zdo.IntValue));
        }
    }
}
