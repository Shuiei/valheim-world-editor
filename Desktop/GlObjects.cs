using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The OpenGL objects one owner made (a view, or the game look of one area), so that all of them can
// be deleted when it lets them go. Avalonia shares each control's context with its own, so buffers,
// textures and programs outlive the control's context: forgetting their names leaked them on the
// graphics card at every visit of the map or of another area. Deleting a name twice, or one this
// owner never made, could delete another's object since names are reused: so each goes through here.
public sealed class GlObjects
{
	private readonly GL _gl;
	private readonly HashSet<uint> _buffers = new(), _arrays = new(), _textures = new(), _programs = new(),
		_framebuffers = new(), _renderbuffers = new();

	public GlObjects(GL gl)
	{
		_gl = gl;
	}

	public uint Buffer() => Keep(_buffers, _gl.GenBuffer());
	public uint VertexArray() => Keep(_arrays, _gl.GenVertexArray());
	public uint Texture() => Keep(_textures, _gl.GenTexture());
	public uint Framebuffer() => Keep(_framebuffers, _gl.GenFramebuffer());
	public uint Renderbuffer() => Keep(_renderbuffers, _gl.GenRenderbuffer());
	// A program linked elsewhere, now this owner's.
	public uint Program(uint p) => Keep(_programs, p);

	public void DeleteBuffer(uint b)
	{
		if (_buffers.Remove(b))
		{
			_gl.DeleteBuffer(b);
		}
	}

	public void DeleteVertexArray(uint a)
	{
		if (_arrays.Remove(a))
		{
			_gl.DeleteVertexArray(a);
		}
	}

	public void DeleteTexture(uint t)
	{
		if (_textures.Remove(t))
		{
			_gl.DeleteTexture(t);
		}
	}

	public void DeleteFramebuffer(uint f)
	{
		if (_framebuffers.Remove(f))
		{
			_gl.DeleteFramebuffer(f);
		}
	}

	public void DeleteRenderbuffer(uint r)
	{
		if (_renderbuffers.Remove(r))
		{
			_gl.DeleteRenderbuffer(r);
		}
	}

	// Everything still held (the owner's context must be current).
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

	private static uint Keep(HashSet<uint> set, uint name)
	{
		set.Add(name);
		return name;
	}
}
