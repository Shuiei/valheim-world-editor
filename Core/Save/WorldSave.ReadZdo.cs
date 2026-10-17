using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>
    /// Reads one object through <see cref="ZdoReader"/>, keeping what the editor lists and recording where things are
    /// in the file so a writer can later copy objects unchanged or patch just the terrain data.
    /// </summary>
    /// <param name="pkg">Reader positioned at the object.</param>
    /// <param name="worldVersion">The world version the object was written in.</param>
    /// <param name="file">The chunk file the bytes belong to.</param>
    /// <param name="replace">Read in place of that object (live: changed in the game), else a new one.</param>
    /// <returns>The object's id.</returns>
    private int ReadZdo(ValheimReader pkg, int worldVersion, ChunkFile file, int replace = -1)
    {
        long start = pkg.Position;
        int id = replace >= 0 ? replace : ObjectRefs.Count;
        ObjectRef objRef = new() { File = file, Start = start };
        if (replace >= 0)
        {
            objRef.LiveId = ObjectRefs[id].LiveId;
            ObjectRefs[id] = objRef;
        }
        else
        {
            ObjectRefs.Add(objRef);
        }
        ZdoReader zdo = new(pkg, worldVersion);
        ushort flags = zdo.Flags;
        Vector3 position = zdo.Position;
        int prefab = zdo.Prefab;
        Vector3 rotation = zdo.Rotation;
        objRef.DataStart = zdo.DataStart;
        objRef.Flags = flags;
        objRef.Prefab = prefab;
        objRef.Position = position;
        bool tracked = prefab == LocationProxyPrefab || ModifierPrefabs.Contains(prefab);
        // Objects that shape the ground (rock4_coast, HeathRockPillar...) are still shown; tools' marks are not.
        bool shown = prefab != LocationProxyPrefab && !(tracked && Terrain.TerrainModifiers.IsGroundMark(prefab));
        int location = 0;
        long timeCreated = 0;
        long creator = 0, owner = 0, playerId = 0;
        // Zero = not stored: the object keeps its prefab's own scale.
        Vector3 scale = Vector3.Zero;
        // The values mods link objects by (App.ObjectLinks): ids, and a part's place and turn in its parent's frame.
        var links = ObjectLinks.Current;
        List<(int Key, int Value)>? linkInts = null;
        List<(int Key, Vector3 Value)>? linkVec3s = null;
        List<(int Key, Quaternion Value)>? linkQuats = null;
        if (!zdo.HasData)
        {
            objRef.End = pkg.Position;
            RememberTemplate(prefab, id);
            file.Objects.Add((start, pkg.Position));
            if (shown)
            {
                Objects.Add((id, prefab, position, rotation, scale));
            }
            if (tracked && prefab != LocationProxyPrefab)
            {
                Placed.Add(new PlacedObject(prefab, 0, position, rotation, 0));
            }
            return id;
        }
        while (zdo.Next())
        {
            int key = zdo.Key;
            switch (zdo.Section)
            {
                case ZdoData.Floats:
                    if (key == ZdoData.ScaleScalarKey)
                    {
                        scale = new Vector3(zdo.FloatValue);
                    }
                    break;
                case ZdoData.Vec3s:
                    if (key == ZdoData.ScaleKey)
                    {
                        scale = zdo.Vec3Value;
                    }
                    else if (links.Vec3Keys.Contains(key))
                    {
                        (linkVec3s ??= []).Add((key, zdo.Vec3Value));
                    }
                    break;
                case ZdoData.Quats:
                    if (links.QuatKeys.Contains(key))
                    {
                        (linkQuats ??= []).Add((key, zdo.QuatValue));
                    }
                    break;
                case ZdoData.Ints:
                    if (prefab == LocationProxyPrefab && key == LocationKey)
                    {
                        Locations.Add((position, zdo.IntValue));
                        location = zdo.IntValue;
                    }
                    else if (key == TamedKey && zdo.IntValue != 0)
                    {
                        Tamed.Add(id);
                    }
                    else if (links.IntKeys.Contains(key))
                    {
                        (linkInts ??= []).Add((key, zdo.IntValue));
                    }
                    break;
                case ZdoData.Longs:
                    if (key == TimeCreatedKey)
                    {
                        timeCreated = zdo.LongValue;
                    }
                    else if (key == Editing.ObjectField.CreatorKey)
                    {
                        creator = zdo.LongValue;
                    }
                    else if (key == OwnerKey)
                    {
                        owner = zdo.LongValue;
                    }
                    else if (key == PlayerIdKey)
                    {
                        playerId = zdo.LongValue;
                    }
                    break;
                case ZdoData.Strings:
                    long who = key == OwnerNameKey ? owner : key == CreatorNameKey ? creator : key == PlayerNameKey ? playerId : 0;
                    if (who != 0 && zdo.StringValue!.Length > 0)
                    {
                        PlayerNames[who] = zdo.StringValue;
                    }
                    break;
                case ZdoData.ByteArrays when key == TCDataKey:
                    TerrainZone zone;
                    try
                    {
                        zone = TerrainZone.Decode(position, prefab, zdo.BytesValue!);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
                    {
                        // Damaged ground data: the game loses only this zone's ground edits. Here the object stays an
                        // ordinary one, kept byte for byte, and the rest of the world reads on.
                        Log.CaughtBrief(ex, $"reading the ground data of the terrain object at {position} (damaged: kept as it is)");
                        break;
                    }
                    zone.Source = new ZdoLocation(file, start, -1, zdo.ValueStart, pkg.Position, flags);
                    TerrainZones.Add(zone);
                    break;
                default:
                    // The connection and byte arrays other than ground data: read past, nothing the loader lists.
                    break;
            }
        }
        long end = pkg.Position;
        objRef.End = end;
        file.Objects.Add((start, end));
        if (tracked && (prefab != LocationProxyPrefab || location != 0))
        {
            Placed.Add(new PlacedObject(prefab, location, position, rotation, timeCreated));
        }
        bool terrain = TerrainZones.Count > 0 && TerrainZones[^1].Source is { } last && last.File == file && last.Start == start;
        if (linkInts != null && ObjectLinks.Read(links, linkInts, linkVec3s, linkQuats) is { } link)
        {
            Links[id] = (link, rotation);
        }
        objRef.IsPiece = creator != 0;
        objRef.Creator = creator;
        objRef.IsTerrain = terrain;
        if (!terrain)
        {
            RememberTemplate(prefab, id);
        }
        if (creator != 0)
        {
            Creators[creator] = Creators.GetValueOrDefault(creator) + 1;
            Pieces.Add((id, prefab, position, rotation.Y));
        }
        else if (shown && !terrain)
        {
            Objects.Add((id, prefab, position, rotation, scale));
        }
        else if (prefab == LocationProxyPrefab && Editing.Uplift.IsDisc(location))
        {
            Discs.Add((id, position));
        }
        else if (prefab == LocationProxyPrefab && Terrain.PrefabCatalog.IsRunestone(location))
        {
            // A runestone: listed under its location's name (see PrefabCatalog.RunestoneLocations).
            Objects.Add((id, location, position, rotation, scale));
        }
        // Only this object's own terrain data can still lack its end (the last zone added, if it is this
        // object's): looking through every zone for each object made loading a large world slow.
        if (terrain && TerrainZones[^1].Source is { End: < 0 } src)
        {
            TerrainZones[^1].Source = src with { End = end };
        }
        return id;
    }
}
