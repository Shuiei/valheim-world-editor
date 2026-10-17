using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// The OpenGL objects one owner made (a view, or the game look of one area), so that all of them can be deleted when it
/// lets them go. Avalonia shares each control's context with its own, so buffers, textures and programs outlive the
/// control's context: forgetting their names leaked them on the graphics card at every visit of the map or of another
/// area. Deleting a name twice, or one this owner never made, could delete another's object since names are reused: so
/// each goes through here.
/// </summary>
public sealed class GlObjects
{
    /// <summary>
    /// The owner's OpenGL.
    /// </summary>
    private readonly GL _gl;
    /// <summary>
    /// The names this owner made and still holds, by kind.
    /// </summary>
    private readonly HashSet<uint> _buffers = [], _arrays = [], _textures = [], _programs = [],
        _framebuffers = [], _renderbuffers = [];

    /// <summary>
    /// An empty set of objects made with this OpenGL.
    /// </summary>
    public GlObjects(GL gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// Makes a buffer this owner holds.
    /// </summary>
    public uint Buffer() => Keep(_buffers, _gl.GenBuffer());
    /// <summary>
    /// Makes a vertex array this owner holds.
    /// </summary>
    public uint VertexArray() => Keep(_arrays, _gl.GenVertexArray());
    /// <summary>
    /// Makes a texture this owner holds.
    /// </summary>
    public uint Texture() => Keep(_textures, _gl.GenTexture());
    /// <summary>
    /// Makes a framebuffer this owner holds.
    /// </summary>
    public uint Framebuffer() => Keep(_framebuffers, _gl.GenFramebuffer());
    /// <summary>
    /// Makes a renderbuffer this owner holds.
    /// </summary>
    public uint Renderbuffer() => Keep(_renderbuffers, _gl.GenRenderbuffer());
    /// <summary>
    /// A program linked elsewhere, now this owner's.
    /// </summary>
    /// <returns>The same name.</returns>
    public uint Program(uint p) => Keep(_programs, p);

    /// <summary>
    /// Makes an indexed triangle mesh with positions only (attribute 0) that this owner holds; no vertex array is bound
    /// after.
    /// </summary>
    /// <param name="v">Positions, x, y, z per corner.</param>
    /// <param name="idx">Three corner indices per triangle.</param>
    /// <returns>Its vertex array and its two buffers.</returns>
    public unsafe (uint Vao, uint Vbo, uint Ebo) PositionMesh(float[] v, uint[] idx)
    {
        uint vao = VertexArray();
        _gl.BindVertexArray(vao);
        uint vbo = Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        Upload(BufferTargetARB.ArrayBuffer, v);
        uint ebo = Buffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        Upload(BufferTargetARB.ElementArrayBuffer, idx);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
        _gl.BindVertexArray(0);
        return (vao, vbo, ebo);
    }

    /// <summary>
    /// Copies an array into the buffer bound to a target, as static data.
    /// </summary>
    /// <typeparam name="T">The element type (unmanaged, as the GL reads it).</typeparam>
    /// <param name="target">The buffer target the buffer is bound to.</param>
    /// <param name="data">The data to copy.</param>
    public unsafe void Upload<T>(BufferTargetARB target, T[] data) where T : unmanaged
    {
        fixed (T* p = data)
        {
            _gl.BufferData(target, (nuint)(data.Length * sizeof(T)), p, BufferUsageARB.StaticDraw);
        }
    }

    /// <summary>
    /// Deletes a buffer if this owner holds it (else nothing: the name may be another's now).
    /// </summary>
    public void DeleteBuffer(uint b) => DeleteHeld(_buffers, b, () => _gl.DeleteBuffer(b));

    /// <summary>
    /// Deletes a vertex array if this owner holds it.
    /// </summary>
    public void DeleteVertexArray(uint a) => DeleteHeld(_arrays, a, () => _gl.DeleteVertexArray(a));

    /// <summary>
    /// Deletes a texture if this owner holds it.
    /// </summary>
    public void DeleteTexture(uint t) => DeleteHeld(_textures, t, () => _gl.DeleteTexture(t));

    /// <summary>
    /// Deletes a framebuffer if this owner holds it.
    /// </summary>
    public void DeleteFramebuffer(uint f) => DeleteHeld(_framebuffers, f, () => _gl.DeleteFramebuffer(f));

    /// <summary>
    /// Deletes a renderbuffer if this owner holds it.
    /// </summary>
    public void DeleteRenderbuffer(uint r) => DeleteHeld(_renderbuffers, r, () => _gl.DeleteRenderbuffer(r));

    /// <summary>
    /// Deletes everything still held (the owner's context must be current); the set is empty after.
    /// </summary>
    public void DeleteAll()
    {
        foreach (uint a in _arrays)
        {
            _gl.DeleteVertexArray(a);
        }
        foreach (uint b in _buffers)
        {
            _gl.DeleteBuffer(b);
        }
        foreach (uint t in _textures)
        {
            _gl.DeleteTexture(t);
        }
        foreach (uint f in _framebuffers)
        {
            _gl.DeleteFramebuffer(f);
        }
        foreach (uint r in _renderbuffers)
        {
            _gl.DeleteRenderbuffer(r);
        }
        foreach (uint p in _programs)
        {
            _gl.DeleteProgram(p);
        }
        _arrays.Clear();
        _buffers.Clear();
        _textures.Clear();
        _framebuffers.Clear();
        _renderbuffers.Clear();
        _programs.Clear();
    }

    /// <summary>
    /// Deletes a name with its delete call only when the set holds it, and takes it out of the set: a name this owner
    /// does not hold is left alone, since it may be another owner's object now.
    /// </summary>
    /// <param name="set">The names this owner holds of one kind.</param>
    /// <param name="name">The name to delete.</param>
    /// <param name="delete">The OpenGL delete call for the name (not made when the set does not hold it, so it may name
    /// OpenGL that is gone).</param>
    internal static void DeleteHeld(HashSet<uint> set, uint name, Action delete)
    {
        if (set.Remove(name))
        {
            delete();
        }
    }

    /// <summary>
    /// Adds a name to a set and returns it.
    /// </summary>
    private static uint Keep(HashSet<uint> set, uint name)
    {
        set.Add(name);
        return name;
    }
}
