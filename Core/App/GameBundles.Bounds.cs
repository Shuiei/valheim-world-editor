namespace TerrainEditor.App;

/// <summary>
/// A mesh's bounds and vertex count, read from the mesh object.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// A mesh's bounds as Unity keeps them (m_LocalAABB: centre and extent, x, y, z each), null when it cannot be read
    /// (to tell apart meshes of the same name; one that cannot be read is not a candidate).
    /// </summary>
    public float[]? BoundsOf(Where mesh)
    {
        lock (_sync)
        {
            try
            {
                if (Read(mesh.Bundle, mesh.PathId) is not { } m || !Has(m, "m_LocalAABB"))
                {
                    return null;
                }
                var c = m["m_LocalAABB.m_Center"];
                var e = m["m_LocalAABB.m_Extent"];
                return [c["x"].AsFloat, c["y"].AsFloat, c["z"].AsFloat, e["x"].AsFloat, e["y"].AsFloat, e["z"].AsFloat];
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
            {
                Log.CaughtBrief(e, "reading a mesh's bounds (not a candidate)");
                return null;
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    /// <summary>
    /// A mesh's vertex count, -1 when it cannot be read (to tell apart meshes of the same name; one that cannot be read
    /// is not a candidate).
    /// </summary>
    public int VertexCountOf(Where mesh)
    {
        lock (_sync)
        {
            try
            {
                return Read(mesh.Bundle, mesh.PathId) is { } m ? (int)m["m_VertexData.m_VertexCount"].AsUInt : -1;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
            {
                Log.CaughtBrief(e, "counting a mesh's vertices (taken as unknown)");
                return -1;
            }
            finally
            {
                _fields.Clear();
            }
        }
    }
}
