using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>One object as it is read: where it is, what it is, and its flags.</summary>
    /// <param name="Id">The object's id.</param>
    /// <param name="Prefab">Its prefab hash.</param>
    /// <param name="Position">Its position.</param>
    /// <param name="Rotation">Its rotation.</param>
    /// <param name="File">The chunk file the bytes belong to.</param>
    /// <param name="Start">Where the object starts in the file.</param>
    /// <param name="Flags">Its ZDO flags.</param>
    /// <param name="Ref">The object's reference in the index.</param>
    /// <param name="Tracked">Whether it is a location proxy or a ground modifier.</param>
    /// <param name="Shown">Whether the editor lists it.</param>
    private sealed record ZdoSite(int Id, int Prefab, Vector3 Position, Vector3 Rotation, ChunkFile File, long Start, ushort Flags, ObjectRef Ref, bool Tracked, bool Shown);

    /// <summary>What an object's data sections give while it is read (filled by the section readers).</summary>
    private sealed class ZdoFields
    {
        /// <summary>A fields holder reading link keys from the given set.</summary>
        /// <param name="links">The mod link keys in use.</param>
        public ZdoFields(LinkSet links) => Links = links;

        /// <summary>The mod link keys in use.</summary>
        public LinkSet Links { get; }

        /// <summary>The object's scale; zero when not stored (it keeps its prefab's own scale).</summary>
        public Vector3 Scale { get; set; } = Vector3.Zero;

        /// <summary>The location index of a location proxy (zero otherwise).</summary>
        public int Location { get; set; }

        /// <summary>The time the object was created.</summary>
        public long TimeCreated { get; set; }

        /// <summary>The piece creator's id (zero when none).</summary>
        public long Creator { get; set; }

        /// <summary>The owner's id.</summary>
        public long Owner { get; set; }

        /// <summary>The player id.</summary>
        public long PlayerId { get; set; }

        /// <summary>The int link values, or null when there are none.</summary>
        public List<(int Key, int Value)>? LinkInts { get; set; }

        /// <summary>The vector link values, or null when there are none.</summary>
        public List<(int Key, Vector3 Value)>? LinkVec3s { get; set; }

        /// <summary>The quaternion link values, or null when there are none.</summary>
        public List<(int Key, Quaternion Value)>? LinkQuats { get; set; }
    }

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
        var site = new ZdoSite(id, prefab, position, rotation, file, start, flags, objRef, tracked, shown);
        if (!zdo.HasData)
        {
            RecordWithoutData(site, pkg.Position);
            return id;
        }
        // The values mods link objects by (App.ObjectLinks): ids, and a part's place and turn in its parent's frame.
        var fields = new ZdoFields(ObjectLinks.Current);
        ReadObjectData(site, fields, zdo, pkg);
        RecordObject(site, fields, pkg.Position);
        return id;
    }

    /// <summary>Reads each data section of an object in turn, handing each entry to the section's reader.</summary>
    /// <param name="site">The object.</param>
    /// <param name="fields">The fields the readers fill.</param>
    /// <param name="zdo">The reader positioned in the object's data.</param>
    /// <param name="pkg">The reader of the file (for where the terrain data ends).</param>
    private void ReadObjectData(ZdoSite site, ZdoFields fields, ZdoReader zdo, ValheimReader pkg)
    {
        while (zdo.Next())
        {
            int key = zdo.Key;
            switch (zdo.Section)
            {
                case ZdoData.Floats:
                    ReadFloatField(fields, zdo, key);
                    break;
                case ZdoData.Vec3s:
                    ReadVec3Field(fields, zdo, key);
                    break;
                case ZdoData.Quats:
                    ReadQuatField(fields, zdo, key);
                    break;
                case ZdoData.Ints:
                    ReadIntField(site, fields, zdo, key);
                    break;
                case ZdoData.Longs:
                    ReadLongField(fields, zdo, key);
                    break;
                case ZdoData.Strings:
                    ReadStringField(fields, zdo, key);
                    break;
                case ZdoData.ByteArrays when key == TCDataKey:
                    ReadTerrainField(site, zdo, pkg);
                    break;
                default:
                    // The connection and byte arrays other than ground data: read past, nothing the loader lists.
                    break;
            }
        }
    }
}
